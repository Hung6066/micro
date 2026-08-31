using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using His.Hope.Bff.Core.Authentication;
using His.Hope.IdentityService.Api.Composition;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Application.Security;
using His.Hope.IdentityService.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.WebUtilities;
using StackExchange.Redis;
using His.Hope.SharedKernel.Authorization;
using His.Hope.SharedKernel.Protocol;

namespace His.Hope.IdentityService.Api.Services;

public sealed record OidcLoginCompletionResult(bool RequiresMfa, string RedirectUrl);
public sealed record AdaptiveMfaMethods(
    string? PreferredMethod,
    IReadOnlyList<string> AvailableMethods,
    bool IsUnfamiliarDevice,
    string RedirectHandle = "/");
public enum PendingMfaCompletionStatus
{
    Success,
    Unauthorized,
    InvalidCode
}
public sealed record PendingMfaCompletionResult(PendingMfaCompletionStatus Status, string? RedirectUrl);
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
    IIdentityService identityService,
    IMfaSecretEncryptor encryptor,
    TotpService totpService,
    IDataProtectionProvider dataProtectionProvider,
    IConnectionMultiplexer redis,
    IConfiguration configuration)
{
    private const string CookieName = HisHopeProtocolConstants.Cookies.OidcMfa;
    private const string SessionCookieName = HisHopeProtocolConstants.Cookies.OidcMfaSession;
    private const string TrustedDeviceCookieName = HisHopeProtocolConstants.Cookies.TrustedDevice;
    private const string BrowserSessionCookieName = HisHopeProtocolConstants.Cookies.BrowserSession;
    private const string BrowserCsrfCookieName = HisHopeProtocolConstants.Cookies.BrowserCsrf;
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
        var safeReturnUrl = ResolveSafeReturnUrl(context, returnUrl);
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
                Domain = BffHelpers.CookieDomain(configuration),
                Path = "/",
                MaxAge = PendingMfaLifetime
            });

            return new(true, $"/Account/Mfa?returnUrl={Uri.EscapeDataString(safeReturnUrl)}");
        }

        await SignInAsync(user, authenticationMethods);
        return new(false, safeReturnUrl);
    }

    public async Task<AdaptiveMfaMethods?> GetPendingMfaMethodsAsync(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        var pending = TryGetPendingMfaContext(context);
        if (pending is null)
            return null;

        var hasPasskey = await db.PasskeyCredentials
            .AsNoTracking()
            .AnyAsync(item => item.UserId == pending.UserId.ToString(), cancellationToken);
        var hasTotp = await db.UserMfas
            .AsNoTracking()
            .AnyAsync(item => item.UserId == pending.UserId && item.IsEnabled, cancellationToken);
        var methods = AdaptiveMfaMethodPolicy.Resolve(
            hasPasskey,
            hasMobileApproval: hasPasskey,
            hasTotp,
            pending.IsUnfamiliarDevice);

        return methods with { RedirectHandle = CreateRedirectHandle(context, pending.ReturnUrl) };
    }

    public async Task<string?> CompleteMfaAsync(HttpContext context, string code, CancellationToken cancellationToken)
    {
        var result = await CompletePendingTotpAsync(context, code, cancellationToken);
        return result.Status == PendingMfaCompletionStatus.Success
            ? result.RedirectUrl
            : null;
    }

    public async Task<PendingMfaCompletionResult> CompletePendingTotpAsync(
        HttpContext context,
        string code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new(PendingMfaCompletionStatus.InvalidCode, null);

        var pending = TryGetPendingMfaContext(context);
        if (pending is null)
            return new(PendingMfaCompletionStatus.Unauthorized, null);

        var user = await userManager.FindByIdAsync(pending.UserId.ToString());
        var mfa = await db.UserMfas.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == pending.UserId, cancellationToken);
        if (user is null || !user.IsActive || mfa is null || !mfa.IsEnabled)
            return new(PendingMfaCompletionStatus.Unauthorized, null);

        var secret = encryptor.Decrypt(mfa.SecretKey);
        if (!totpService.VerifyCode(secret, code.Trim()))
            return new(PendingMfaCompletionStatus.InvalidCode, null);

        var redirectUrl = await CompletePendingMfaAsync(context, pending, user, "otp");
        return new(PendingMfaCompletionStatus.Success, redirectUrl);
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

        return TryResolvePendingMfaContext(
            cookieState.PendingId,
            sessionId,
            expectedUserId: null,
            requiredUserAgentHash: GetUserAgentHash(context));
    }

    public bool HasLivePendingMfaContext(string pendingId, string sessionId, Guid userId)
    {
        return TryResolvePendingMfaContext(
            pendingId,
            sessionId,
            expectedUserId: userId,
            requiredUserAgentHash: null) is not null;
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

        return await CompletePendingMfaAsync(context, pending, user, "passkey");
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

    private async Task SignInAsync(User user, IReadOnlyCollection<string> authenticationMethods)
    {
        var (permissions, tenantClaims) = await HumanSessionAuthClaims.ResolveAsync(
            userManager,
            identityService,
            user);

        var claims = authenticationMethods
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(value => new Claim(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.AuthenticationMethod, value))
            .Append(new Claim(
                "auth_time",
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .Append(new Claim(
                AuthorizationConstants.Claims.PrincipalType,
                AuthorizationConstants.PrincipalTypes.Human))
            .Concat(permissions.Select(permission => new Claim(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Permissions, permission)))
            .Concat(tenantClaims)
            .ToArray();

        await signInManager.SignInWithClaimsAsync(user, isPersistent: false, additionalClaims: claims);
    }

    private async Task<string> CompletePendingMfaAsync(
        HttpContext context,
        PendingMfaContext pending,
        User user,
        string authenticationMethod)
    {
        await DeletePendingMfaAsync(context, pending.PendingId);
        await SignInAsync(
            user,
            pending.AuthenticationMethods
                .Append(authenticationMethod)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
        return pending.ReturnUrl;
    }

    private string ResolveSafeReturnUrl(HttpContext context, string? value) =>
        AuthenticationRedirectValidator.ResolveSafeReturnUrl(
            value,
            configuration,
            context.Request.Headers.Referer.FirstOrDefault(),
            ReadSpaOriginHint(context));

    private static string? ReadSpaOriginHint(HttpContext context)
    {
        if (context.Request.HasFormContentType)
        {
            var formValue = context.Request.Form["spaOrigin"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(formValue))
                return formValue;
        }

        return context.Request.Query["spaOrigin"].FirstOrDefault();
    }

    private string CreateRedirectHandle(HttpContext context, string returnUrl)
    {
        var safeReturnUrl = ResolveSafeReturnUrl(context, returnUrl);
        var delimiterIndex = safeReturnUrl.IndexOfAny(['?', '#']);
        return delimiterIndex >= 0 ? safeReturnUrl[..delimiterIndex] : safeReturnUrl;
    }

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
                  AppendBrowserCsrfCookie(context, existingSession.CsrfToken);
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
           Domain = BffHelpers.CookieDomain(configuration),
           Path = "/",
              MaxAge = PendingMfaLifetime
          });
        AppendBrowserCsrfCookie(context, pendingSession.CsrfToken);

          return (sessionId, false);
      }

      private void AppendBrowserCsrfCookie(HttpContext context, string csrfToken)
      {
          if (string.IsNullOrWhiteSpace(csrfToken))
              return;

          context.Response.Cookies.Append(BrowserCsrfCookieName, csrfToken, new CookieOptions
          {
              HttpOnly = false,
              Secure = context.Request.IsHttps,
              SameSite = SameSiteMode.Strict,
              Domain = BffHelpers.CookieDomain(configuration),
              Path = "/",
              MaxAge = PendingMfaLifetime
          });
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
        return ParseLiveSession(rawSession);
    }

    private async Task DeletePendingMfaAsync(HttpContext context, string pendingId)
    {
        await redisDb.KeyDeleteAsync(GetPendingMfaKey(pendingId));
        DeletePendingMfaCookies(context);
    }

    private PendingMfaContext? TryResolvePendingMfaContext(
        string pendingId,
        string sessionId,
        Guid? expectedUserId,
        string? requiredUserAgentHash)
    {
        var pending = TryReadPendingMfaSession(pendingId);
        if (pending is null)
            return null;

        if (expectedUserId.HasValue && pending.UserId != expectedUserId.Value)
            return null;

        if (!string.Equals(sessionId, pending.SessionId, StringComparison.Ordinal))
            return null;

        if (requiredUserAgentHash is not null &&
            !string.Equals(requiredUserAgentHash, pending.UserAgentHash, StringComparison.Ordinal))
            return null;

        var session = ParseLiveSession(redisDb.StringGet(GetBrowserSessionKey(sessionId)));
        if (session is null)
            return null;

        if (!string.Equals(session.UserId, pending.UserId.ToString(), StringComparison.OrdinalIgnoreCase))
            return null;

        if (requiredUserAgentHash is not null &&
            !string.Equals(session.UserAgentHash, requiredUserAgentHash, StringComparison.Ordinal))
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

    private PendingMfaSessionRecord? TryReadPendingMfaSession(string pendingId)
    {
        var rawPending = redisDb.StringGet(GetPendingMfaKey(pendingId));
        if (!rawPending.HasValue)
            return null;

        try
        {
            var pending = PendingMfaSessionRecord.FromJson(rawPending!);
            return DateTimeOffset.UtcNow - pending.CreatedAt <= PendingMfaLifetime
                ? pending
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static SessionData? ParseLiveSession(RedisValue rawSession)
    {
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

    private static string GetPendingMfaKey(string pendingId) => $"hishop:oidc-mfa:pending:{pendingId}";

    private static string GetBrowserSessionKey(string sessionId) => $"session:{sessionId}";

    private static string GetUserAgentHash(HttpContext context) =>
        BffHelpers.ComputeSha256(context.Request.Headers.UserAgent.ToString());

    private void DeletePendingMfaCookies(HttpContext context)
    {
        context.Response.Cookies.Delete(CookieName, new CookieOptions { Domain = BffHelpers.CookieDomain(configuration), Path = "/" });
        context.Response.Cookies.Delete(SessionCookieName, new CookieOptions { Domain = BffHelpers.CookieDomain(configuration), Path = "/" });
    }
}
