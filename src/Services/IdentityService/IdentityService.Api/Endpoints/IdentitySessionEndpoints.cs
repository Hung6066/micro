using System.Security.Claims;
using System.Text.Json;
using His.Hope.Bff.Core.Authentication;
using His.Hope.Contracts;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.IdentityService.Api.Composition;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.Security;
using His.Hope.SharedKernel.Protocol;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class IdentitySessionEndpoints
{
    public static void MapIdentitySessionEndpoints(this RouteGroupBuilder auth)
    {
        auth.MapPost("/refresh", async (RefreshTokenRequest request, IIdentityService identityService,
            IConnectionMultiplexer redis, SessionTokenProtector tokenProtector,
            HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
        {
            try
            {
                var sessionId = httpContext.Request.Cookies[HisHopeProtocolConstants.Cookies.BrowserSession];
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    var sessionJson = await redis.GetDatabase().StringGetAsync($"session:{sessionId}");
                    if (sessionJson.HasValue)
                    {
                        var session = JsonSerializer.Deserialize<SessionData>(sessionJson!);
                        var csrfToken = httpContext.Request.Headers["X-CSRF-Token"].FirstOrDefault();
                        if (session is not null && !string.Equals(session.CsrfToken, csrfToken, StringComparison.Ordinal))
                            return Results.Forbid();
                    }
                }

                if (string.IsNullOrWhiteSpace(request.RefreshToken))
                {
                    if (!string.IsNullOrWhiteSpace(sessionId))
                    {
                        var sessionJson = await redis.GetDatabase().StringGetAsync($"session:{sessionId}");
                        if (sessionJson.HasValue)
                        {
                            var session = JsonSerializer.Deserialize<SessionData>(sessionJson!);
                            if (session is not null && !session.IsExpired && !string.IsNullOrWhiteSpace(session.RefreshToken))
                            {
                                request = request with
                                {
                                    AccessToken = tokenProtector.Unprotect(session.Jwt),
                                    RefreshToken = tokenProtector.Unprotect(session.RefreshToken!)
                                };
                            }
                        }
                    }
                }

                var result = await identityService.RefreshTokenAsync(request, ct);
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Problem(statusCode: 401, extensions: new Dictionary<string, object?> { ["errorCode"] = ApiErrorCodes.AuthenticationRejected });
            }
        })
        .WithDeprecationNotice()
        .WithOpenApi()
        .AllowAnonymous();

        auth.MapPost("/logout", async (IConnectionMultiplexer redis, HttpContext httpContext,
            IIdentityService identityService, IUserSessionTracker sessionTracker,
            ITokenBlacklistService tokenBlacklist, SignInManager<User> signInManager,
            SessionTokenProtector tokenProtector,
            IConfiguration configuration, ILogger<Program> logger, CancellationToken ct) =>
        {
            var sessionId = httpContext.Request.Cookies[HisHopeProtocolConstants.Cookies.BrowserSession];
            string? refreshToken = null;
            string? userId = null;

            if (!string.IsNullOrEmpty(sessionId))
            {
                var db = redis.GetDatabase();
                var sessionJson = await db.StringGetAsync($"session:{sessionId}");
                if (sessionJson.HasValue)
                {
                    var session = JsonSerializer.Deserialize<SessionData>(sessionJson!);
                    if (session is not null)
                    {
                        refreshToken = tokenProtector.UnprotectOptional(session.RefreshToken);
                        userId = session.UserId;
                    }
                }
            }

            // Fallback for SPA flow: extract userId from JWT Bearer token (no BFF session cookie)
            if (string.IsNullOrWhiteSpace(userId))
            {
                var authHeader = httpContext.Request.Headers.Authorization.ToString();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var jwt = authHeader["Bearer ".Length..];
                    userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? httpContext.User.FindFirstValue(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject);
                    logger.LogDebug("Logout via JWT Bearer: UserId={UserId}", userId);
                }
            }

            // Revoke refresh token
            if (!string.IsNullOrWhiteSpace(refreshToken))
                await identityService.LogoutAsync(refreshToken, ct);

            // Clear the central ASP.NET Identity cookie too, so all OIDC SPA clients
            // observe the same SSO logout state through /session-status.
            await signInManager.SignOutAsync();

            // Revoke ALL sessions for this user (cross-port logout)
            if (!string.IsNullOrWhiteSpace(userId))
            {
                // Blacklist all user tokens at user level (checked by JWT validation)
                await tokenBlacklist.RevokeAllUserTokensAsync(userId, ct);

                // Delete all Redis sessions for this user
                var sessions = await sessionTracker.GetUserSessionsAsync(userId);
                if (sessions.Length > 0)
                {
                    var db = redis.GetDatabase();
                    var keys = sessions.Select(s => (RedisKey)$"session:{s}").ToArray();
                    await db.KeyDeleteAsync(keys);
                }

                // Clean up the user session set
                await sessionTracker.ClearUserSessionsAsync(userId);

                logger.LogInformation(
                    "Cross-port logout: UserId={UserId}, sessions cleared={SessionCount}",
                    userId, sessions.Length);
            }

            // Clear cookies
            httpContext.Response.Cookies.Append(HisHopeProtocolConstants.Cookies.BrowserSession, "", new CookieOptions
            {
                HttpOnly = true,
                Secure = httpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Domain = BffHelpers.CookieDomain(configuration),
                Path = "/",
                Expires = DateTimeOffset.UnixEpoch
            });
            httpContext.Response.Cookies.Append("hishop_csrf", "", new CookieOptions
            {
                HttpOnly = false,
                Secure = httpContext.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Domain = BffHelpers.CookieDomain(configuration),
                Path = "/",
                Expires = DateTimeOffset.UnixEpoch
            });

            return Results.NoContent();
        })
        .WithDeprecationNotice()
        .WithOpenApi()
        .RequireRateLimiting("auth")
        .AllowAnonymous();
    }
}
