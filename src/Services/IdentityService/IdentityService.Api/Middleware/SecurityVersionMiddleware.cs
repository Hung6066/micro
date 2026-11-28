using System.Security.Claims;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Middleware;

/// <summary>
/// Rejects credentials issued before the account's security stamp changed
/// (password change or reset, MFA change, forced sign-out) and credentials of
/// deactivated accounts. Bearer tokens are checked by the middleware; the SSO
/// cookie is checked by <see cref="ValidateCookiePrincipalAsync"/>.
/// </summary>
public sealed class SecurityVersionMiddleware(RequestDelegate next)
{
    private const string IdentitySecurityStampClaimType = "AspNet.Identity.SecurityStamp";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var subject = context.User.FindFirstValue(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject) ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var presented = context.User.FindFirstValue("securityVersion");
        if (!Guid.TryParse(subject, out var userId) || string.IsNullOrWhiteSpace(presented))
        {
            await next(context);
            return;
        }

        var current = await ResolveCurrentStampAsync(context, userId);
        if (current is null || !string.Equals(current, presented, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }

    /// <summary>
    /// Cookie <c>OnValidatePrincipal</c> handler. Without it the SSO cookie stays
    /// valid after a password reset, deactivation or "sign out everywhere" and
    /// can keep minting new sessions and authorization codes.
    /// </summary>
    public static async Task ValidateCookiePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var presented = principal?.FindFirstValue(IdentitySecurityStampClaimType);
        var current = Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? await ResolveCurrentStampAsync(context.HttpContext, userId)
            : null;
        if (current is not null && string.Equals(current, presented, StringComparison.Ordinal))
            return;

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }

    /// <returns>The security stamp of an active account, or null when the account is missing or disabled.</returns>
    private static async Task<string?> ResolveCurrentStampAsync(HttpContext context, Guid userId)
    {
        var cache = context.RequestServices.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var cacheKey = $"hishop:identity:security-version:{userId}";
        var cached = await cache.StringGetAsync(cacheKey);
        if (!cached.IsNullOrEmpty)
            return cached.ToString();

        var db = context.RequestServices.GetRequiredService<IdentityDbContext>();
        var current = await db.Users.AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive)
            .Select(user => user.SecurityStamp)
            .SingleOrDefaultAsync(context.RequestAborted);
        if (string.IsNullOrEmpty(current))
            return null;

        await cache.StringSetAsync(cacheKey, current, CacheDuration);
        return current;
    }
}
