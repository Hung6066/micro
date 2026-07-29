using System.Security.Claims;
using System.Text.Json;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Services;

public sealed record OidcLoginCompletionResult(bool RequiresMfa, string RedirectUrl);

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
    IDataProtectionProvider dataProtectionProvider)
{
    private const string CookieName = "hishop_oidc_mfa";
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("HisHope.OidcMfa.v1");

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
            var pending = new PendingMfa(user.Id, safeReturnUrl, authenticationMethods.ToArray(), DateTimeOffset.UtcNow);
            var protectedState = protector.Protect(JsonSerializer.Serialize(pending));
            context.Response.Cookies.Append(CookieName, protectedState, new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = TimeSpan.FromMinutes(5)
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

        var protectedState = context.Request.Cookies[CookieName];
        if (string.IsNullOrWhiteSpace(protectedState))
            return null;

        PendingMfa? pending;
        try
        {
            pending = JsonSerializer.Deserialize<PendingMfa>(protector.Unprotect(protectedState));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        if (pending is null || DateTimeOffset.UtcNow - pending.CreatedAt > TimeSpan.FromMinutes(5))
            return null;

        var user = await userManager.FindByIdAsync(pending.UserId.ToString());
        var mfa = await db.UserMfas.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == pending.UserId, cancellationToken);
        if (user is null || !user.IsActive || mfa is null || !mfa.IsEnabled)
            return null;

        var secret = encryptor.Decrypt(mfa.SecretKey);
        if (!totpService.VerifyCode(secret, code.Trim()))
            return null;

        context.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
        await SignInAsync(user, pending.AuthenticationMethods.Append("otp").Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        return pending.ReturnUrl;
    }

    public Guid? TryGetPendingMfaUserId(HttpContext context)
    {
        var pending = ReadPendingMfa(context);
        return pending?.UserId;
    }

    public async Task<string?> CompleteMfaWithPasskeyAsync(
        HttpContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var pending = ReadPendingMfa(context);
        if (pending is null || pending.UserId != userId ||
            DateTimeOffset.UtcNow - pending.CreatedAt > TimeSpan.FromMinutes(5))
            return null;

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
            return null;

        context.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
        await SignInAsync(user, pending.AuthenticationMethods.Append("passkey").Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        return pending.ReturnUrl;
    }

    private PendingMfa? ReadPendingMfa(HttpContext context)
    {
        var protectedState = context.Request.Cookies[CookieName];
        if (string.IsNullOrWhiteSpace(protectedState))
            return null;

        try
        {
            return JsonSerializer.Deserialize<PendingMfa>(protector.Unprotect(protectedState));
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

    private sealed record PendingMfa(Guid UserId, string ReturnUrl, string[] AuthenticationMethods, DateTimeOffset CreatedAt);
}
