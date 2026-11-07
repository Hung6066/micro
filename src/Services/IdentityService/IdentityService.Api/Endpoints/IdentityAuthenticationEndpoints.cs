using System.Security.Claims;
using System.Text.Json;
using His.Hope.Bff.Core.Authentication;
using His.Hope.Contracts;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Application.Security;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.IdentityService.Api.Composition;
using His.Hope.IdentityService.Api.Services;
using His.Hope.Infrastructure.Caching;
using His.Hope.SharedKernel.Authorization;
using His.Hope.SharedKernel.Protocol;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class IdentityAuthenticationEndpoints
{
    public static void MapIdentityAuthenticationEndpoints(this RouteGroupBuilder auth)
    {
        auth.MapPost("/login", async (LoginRequest request, IIdentityService identityService,
            UserManager<User> userManager, SignInManager<User> signInManager,
            JwtTokenGenerator tokenGenerator, IConnectionMultiplexer redis, SessionTokenProtector tokenProtector,
            IUserSessionTracker sessionTracker,
            IConfiguration configuration, HttpContext httpContext, CancellationToken ct) =>
        {
            try
            {
                var result = await identityService.LoginAsync(request, ct);
                var identityUser = await userManager.FindByIdAsync(result.User.Id.ToString());
                if (identityUser is null)
                    return Results.Unauthorized();

                if (identityUser.TwoFactorEnabled)
                {
                    return Results.Json(new
                    {
                        error = "mfa_required",
                        errorDescription = "MFA verification is required before a session can be issued.",
                        requiresMfa = true
                    }, statusCode: StatusCodes.Status401Unauthorized);
                }

                var roles = await userManager.GetRolesAsync(identityUser);
                var (effectivePermissions, tenantClaims) = await HumanSessionAuthClaims.ResolveAsync(
                    userManager, identityService, identityUser, ct);
                var permissions = effectivePermissions.ToArray();
                var legacyAuthTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
                var (sessionJwt, sessionExpiresAt) = tokenGenerator.GenerateAccessToken(
                    identityUser,
                    roles,
                    permissions,
                    amrValues: ["pwd"],
                    additionalClaims: tenantClaims.Append(new System.Security.Claims.Claim("auth_time", legacyAuthTime, ClaimValueTypes.Integer64)));
                var identityPrincipal = await signInManager.CreateUserPrincipalAsync(identityUser);
                if (identityPrincipal.Identity is ClaimsIdentity identityClaims)
                {
                    identityClaims.AddClaim(new System.Security.Claims.Claim(
                        AuthorizationConstants.Claims.PrincipalType,
                        AuthorizationConstants.PrincipalTypes.Human));
                    identityClaims.AddClaim(new System.Security.Claims.Claim(HisHopeProtocolConstants.Claims.AuthenticationMethod, "pwd"));
                        identityClaims.AddClaim(new System.Security.Claims.Claim(
                        "auth_time",
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)));
                    foreach (var permission in permissions)
                        identityClaims.AddClaim(new System.Security.Claims.Claim(HisHopeProtocolConstants.Claims.Permissions, permission));
                    foreach (var tenantClaim in tenantClaims)
                        identityClaims.AddClaim(tenantClaim);
                }
                await httpContext.SignInAsync(
                    IdentityConstants.ApplicationScheme,
                    identityPrincipal,
                    new AuthenticationProperties { IsPersistent = true });

                var sessionId = Guid.NewGuid().ToString("N");
                var csrfToken = Guid.NewGuid().ToString("N");
                var sessionIssuedAt = DateTimeOffset.UtcNow;
                var isPrivileged = configuration.GetSection("Identity:SuperAdmin:UserIds").Get<string[]>()
                    ?.Any(id => string.Equals(id, result.User.Id.ToString(), StringComparison.OrdinalIgnoreCase)) == true;
                var sessionData = new SessionData
                {
                    UserId = result.User.Id.ToString(),
                    Jwt = tokenProtector.Protect(sessionJwt),
                    RefreshToken = tokenProtector.Protect(result.RefreshToken),
                    Permissions = permissions,
                    PrincipalType = AuthorizationConstants.PrincipalTypes.Human,
                    CsrfToken = csrfToken,
                    UserAgentHash = BffHelpers.ComputeSha256(httpContext.Request.Headers.UserAgent.ToString()),
                    IssuedAt = sessionIssuedAt,
                    ExpiresAt = sessionExpiresAt,
                    IsPrivileged = isPrivileged,
                    IdleExpiresAt = sessionIssuedAt.Add(isPrivileged ? TimeSpan.FromMinutes(15) : TimeSpan.FromMinutes(30)),
                    AbsoluteExpiresAt = sessionIssuedAt.Add(isPrivileged ? TimeSpan.FromHours(4) : TimeSpan.FromHours(8))
                };

                await redis.GetDatabase().StringSetAsync(
                    $"session:{sessionId}",
                    JsonSerializer.Serialize(sessionData),
                    TimeSpan.FromHours(1));
                await sessionTracker.AddSessionAsync(result.User.Id.ToString(), sessionId);

                httpContext.Response.Cookies.Append(HisHopeProtocolConstants.Cookies.BrowserSession, sessionId, new CookieOptions
                {
                    HttpOnly = true, Secure = httpContext.Request.IsHttps, SameSite = SameSiteMode.Lax,
                    Domain = BffHelpers.CookieDomain(configuration), Path = "/", MaxAge = TimeSpan.FromHours(1)
                });
                httpContext.Response.Cookies.Append("hishop_csrf", csrfToken, new CookieOptions
                {
                    HttpOnly = false, Secure = httpContext.Request.IsHttps, SameSite = SameSiteMode.Strict,
                    Domain = BffHelpers.CookieDomain(configuration), Path = "/", MaxAge = TimeSpan.FromHours(1)
                });

                return Results.Ok(new { status = "ok", userId = result.User.Id, requiresMfa = false });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Problem(statusCode: 401, extensions: new Dictionary<string, object?>
                { ["errorCode"] = ApiErrorCodes.AuthenticationRejected });
            }
        })
        .WithDeprecationNotice()
        .WithOpenApi()
        .RequireRateLimiting("auth")
        .AllowAnonymous();
    }
}
