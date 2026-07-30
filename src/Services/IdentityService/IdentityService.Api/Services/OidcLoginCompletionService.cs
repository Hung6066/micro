using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using His.Hope.Bff.Core.Authentication;
using His.Hope.IdentityService.Api.Composition;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.WebUtilities;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Services;

public sealed record OidcLoginCompletionResult(bool RequiresMfa, string RedirectUrl);
public sealed record AdaptiveMfaMethods(string? PreferredMethod, IReadOnlyList<string> AvailableMethods, bool IsUnfamiliarDevice);
public sealed record PendingMfaContext(
    string PendingId,
    Guid UserId,
    string ReturnUrl,
    bool IsUnfamiliarDevice,
    string[] AuthenticationMethods,
    DateTimeOffset CreatedAt,
    string SessionId);

public sealed record PendingMfaCookieState(string PendingId);

public sealed record PendingMfaSessionRecord(
    string PendingId,
    Guid UserId,
    string ReturnUrl,
    bool IsUnfamiliarDevice,
    string[] AuthenticationMethods,
    DateTimeOffset CreatedAt,
    string SessionId,
    string UserAgentHash)
{
    public string ToJson() => JsonSerializer.Serialize(this);

    public static PendingMfaSessionRecord FromJson(string json) =>
        JsonSerializer.Deserialize<PendingMfaSessionRecord>(json)
        ?? throw new JsonException("Pending MFA session record could not be deserialized.");
}

public static class AdaptiveMfaMethodPolicy
{
    public static AdaptiveMfaMethods Resolve(
        bool hasPasskey,
        bool hasMobileApproval,
        bool hasTotp,
        bool unfamiliarDevice)
    {
        var available = new List<string>();
        if (hasPasskey) available.Add("passkey");
        if (hasMobileApproval) available.Add("mobileApproval");
        if (hasTotp) available.Add("totp");
        var preferred = unfamiliarDevice && hasMobileApproval
            ? "mobileApproval"
            : hasPasskey ? "passkey"
            : hasMobileApproval ? "mobileApproval"
            : hasTotp ? "totp" : null;

        return new(preferred, available, unfamiliarDevice);
    }
}

/// <summary>
/// Completes every interactive authentication method through the same OIDC
/// session boundary. Primary authentication never receives an OIDC code until
/// the optional MFA step has completed.
/// </summary>
public sealed class OidcLoginCompletionService(
    SignInManager<User> signInManager,
    UserManager<User> userManager,
    IdentityDbContext db,
    IMfaSecretEncryptor encryptor,
    TotpService totpService,
    IDataProtectionProvider dataProtectionProvider,
    IConnectionMultiplexer redis)
{
    private const string CookieName = "hishop_oidc_mfa";
    private const string SessionCookieName = "hishop_oidc_mfa_session";
    private const string TrustedDeviceCookieName = "hishop_trusted_device";
    private const string BrowserSessionCookieName = "hishop_sid";
    private static readonly TimeSpan PendingMfaLifetime = TimeSpan.FromMinutes(5);
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("HisHope.OidcMfa.v1");
    private readonly IDatabase redisDb = redis.GetDatabase();

    public async Task<OidcLoginCompletionResult> CompletePrimaryAsync(
        HttpContext context,
        User user,
        string? returnUrl,
        IReadOnlyCollection<string> authenticationMethods,
        CancellationToken cancellationToken = default)
    {
        var safeReturnUrl = SafeReturnUrl(returnUrl);
        var mfaEnabled = user.TwoFactorEnabled || await db.UserMfas
            .AsNoTracking()
            .AnyAsync(item => item.UserId == user.Id && item.IsEnabled, cancellationToken);
        if (mfaEnabled)
        {
            var sessionBinding = await GetOrCreatePendingSessionAsync(context, user);
            var pendingId = CreateOpaqueToken();
            var pending = new PendingMfaSessionRecord(
                pendingId,
                user.Id,
                safeReturnUrl,
                IsUnfamiliarDevice(context, user, sessionBinding.IsRecognized),
                authenticationMethods.ToArray(),
                DateTimeOffset.UtcNow,
                sessionBinding.SessionId,
                GetUserAgentHash(context));
            await redisDb.StringSetAsync(GetPendingMfaKey(pendingId), pending.ToJson(), PendingMfaLifetime);

            var protectedState = protector.Protect(JsonSerializer.Serialize(new PendingMfaCookieState(pendingId)));
            context.Response.Cookies.Append(CookieName, protectedState, new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = PendingMfaLifetime
            });

            return new(true, $"/Account/Mfa?returnUrl={Uri.EscapeDataString(safeReturnUrl)}");
        }

        await SignInAsync(user, authenticationMethods);
        return new(false, safeReturnUrl);
    }

    public async Task<string?> CompleteMfaAsync(HttpContext context, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var pending = TryGetPendingMfaContext(context);
        if (pending is null)
            return null;

        var user = await userManager.FindByIdAsync(pending.UserId.ToString());
        var mfa = await db.UserMfas.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == pending.UserId, cancellationToken);
        if (user is null || !user.IsActive || mfa is null || !mfa.IsEnabled)
            return null;

        var secret = encryptor.Decrypt(mfa.SecretKey);
        if (!totpService.VerifyCode(secret, code.Trim()))
            return null;

        await DeletePendingMfaAsync(context, pending.PendingId);
        await SignInAsync(user, pending.AuthenticationMethods.Append("otp").Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        return pending.ReturnUrl;
    }

    public Guid? TryGetPendingMfaUserId(HttpContext context)
    {
        return TryGetPendingMfaContext(context)?.UserId;
    }

    public PendingMfaContext? TryGetPendingMfaContext(HttpContext context)
    {
        var cookieState = ReadPendingMfaCookie(context);
        if (cookieState is null)
            return null;

        var sessionId = context.Request.Cookies[BrowserSessionCookieName];
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;

        var rawPending = redisDb.StringGet(GetPendingMfaKey(cookieState.PendingId));
        if (!rawPending.HasValue)
            return null;

        PendingMfaSessionRecord pending;
        try
        {
            pending = PendingMfaSessionRecord.FromJson(rawPending!);
        }
        catch
        {
            return null;
        }

        if (DateTimeOffset.UtcNow - pending.CreatedAt > PendingMfaLifetime)
            return null;

        if (!string.Equals(sessionId, pending.SessionId, StringComparison.Ordinal))
            return null;

        if (!string.Equals(GetUserAgentHash(context), pending.UserAgentHash, StringComparison.Ordinal))
            return null;

        var rawSession = redisDb.StringGet(GetBrowserSessionKey(sessionId));
        if (!rawSession.HasValue)
            return null;

        SessionData session;
        try
        {
            session = JsonSerializer.Deserialize<SessionData>(rawSession!)!;
        }
        catch
        {
            return null;
        }

        if (session is null || session.IsExpired)
            return null;

        if (!string.Equals(session.UserId, pending.UserId.ToString(), StringComparison.OrdinalIgnoreCase))
            return null;

        if (!string.Equals(session.UserAgentHash, pending.UserAgentHash, StringComparison.Ordinal))
            return null;

        return new(
            pending.PendingId,
            pending.UserId,
            pending.ReturnUrl,
            pending.IsUnfamiliarDevice,
            pending.AuthenticationMethods,
            pending.CreatedAt,
            pending.SessionId);
    }

    public async Task<string?> CompleteMfaWithPasskeyAsync(
        HttpContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var pending = TryGetPendingMfaContext(context);
        if (pending is null || pending.UserId != userId)
            return null;

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
            return null;

        await DeletePendingMfaAsync(context, pending.PendingId);
        await SignInAsync(user, pending.AuthenticationMethods.Append("passkey").Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        return pending.ReturnUrl;
    }

    private PendingMfaCookieState? ReadPendingMfaCookie(HttpContext context)
    {
        var protectedState = context.Request.Cookies[CookieName];
        if (string.IsNullOrWhiteSpace(protectedState))
            return null;

        try
        {
            return JsonSerializer.Deserialize<PendingMfaCookieState>(protector.Unprotect(protectedState));
        }
        catch
        {
            return null;
        }
    }

    private Task SignInAsync(User user, IReadOnlyCollection<string> authenticationMethods) =>
        signInManager.SignInWithClaimsAsync(
            user,
            isPersistent: false,
            additionalClaims: authenticationMethods
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(value => new Claim("amr", value)));

    private static string SafeReturnUrl(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.StartsWith("/", StringComparison.Ordinal) &&
        !value.StartsWith("//", StringComparison.Ordinal) && !value.Contains('\\') && !value.Contains(':')
            ? value
            : "/";

    private async Task<(string SessionId, bool IsRecognized)> GetOrCreatePendingSessionAsync(
        HttpContext context,
        User user)
    {
        var existingBrowserSession = context.Request.Cookies[BrowserSessionCookieName];
        if (!string.IsNullOrWhiteSpace(existingBrowserSession))
        {
            var existingSession = await TryGetLiveSessionAsync(existingBrowserSession);
            if (existingSession is not null
                && string.Equals(existingSession.UserId, user.Id.ToString(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(existingSession.UserAgentHash, GetUserAgentHash(context), StringComparison.Ordinal))
            {
                return (existingBrowserSession, true);
            }
        }

        var sessionId = CreateOpaqueToken();
        var now = DateTimeOffset.UtcNow;
        var pendingSession = new SessionData
        {
            UserId = user.Id.ToString(),
            Jwt = string.Empty,
            RefreshToken = null,
            Permissions = [],
            CsrfToken = CreateOpaqueToken(),
            UserAgentHash = GetUserAgentHash(context),
            IssuedAt = now,
            ExpiresAt = now.Add(PendingMfaLifetime)
        };
        await redisDb.StringSetAsync(
            GetBrowserSessionKey(sessionId),
            JsonSerializer.Serialize(pendingSession),
            PendingMfaLifetime);
        context.Response.Cookies.Append(BrowserSessionCookieName, sessionId, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = PendingMfaLifetime
        });

        return (sessionId, false);
    }

    private static string CreateOpaqueToken() =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static bool IsUnfamiliarDevice(
        HttpContext context,
        User user,
        bool hasRecognizedBrowserSession)
    {
        var trustedDeviceToken = user.TrustedDeviceToken?.Trim();
        if (!string.IsNullOrWhiteSpace(trustedDeviceToken))
        {
            var presentedToken = context.Request.Cookies[TrustedDeviceCookieName];
            return !string.Equals(presentedToken, trustedDeviceToken, StringComparison.Ordinal);
        }

        return !hasRecognizedBrowserSession;
    }

    private async Task<SessionData?> TryGetLiveSessionAsync(string sessionId)
    {
        var rawSession = await redisDb.StringGetAsync(GetBrowserSessionKey(sessionId));
        if (!rawSession.HasValue)
            return null;

        try
        {
            var session = JsonSerializer.Deserialize<SessionData>(rawSession!);
            return session is not null && !session.IsExpired ? session : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task DeletePendingMfaAsync(HttpContext context, string pendingId)
    {
        await redisDb.KeyDeleteAsync(GetPendingMfaKey(pendingId));
        DeletePendingMfaCookies(context);
    }

    private static string GetPendingMfaKey(string pendingId) => $"hishop:oidc-mfa:pending:{pendingId}";

    private static string GetBrowserSessionKey(string sessionId) => $"session:{sessionId}";

    private static string GetUserAgentHash(HttpContext context) =>
        BffHelpers.ComputeSha256(context.Request.Headers.UserAgent.ToString());

    private static void DeletePendingMfaCookies(HttpContext context)
    {
        context.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
        context.Response.Cookies.Delete(SessionCookieName, new CookieOptions { Path = "/" });
    }
}
