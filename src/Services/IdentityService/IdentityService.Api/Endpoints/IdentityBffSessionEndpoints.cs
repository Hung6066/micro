using System.Security.Claims;
using System.Text.Json;
using His.Hope.Bff.Core.Authentication;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Api.Composition;
using His.Hope.IdentityService.Api.Services;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.Security;
using His.Hope.SharedKernel.Protocol;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class IdentityBffSessionEndpoints
{
    private sealed record BffSessionExchangeRequest(string? ClientId);

    public static void MapIdentityBffSessionEndpoints(this RouteGroupBuilder auth)
    {
        auth.MapGet("/session-status", async (HttpContext httpContext) =>
        {
            var result = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            return Results.Ok(new
            {
                authenticated = result.Succeeded,
                userName = result.Principal?.Identity?.Name,
                    portalClass = result.Principal?.FindFirst(HisHopeProtocolConstants.Claims.PortalClass)?.Value
            });
        })
        .WithOpenApi()
        .AllowAnonymous();

        // SPA OIDC callback -> BFF session bridge. The browser may have a valid
        // OpenIddict access token without the legacy hishop_sid cookie; mint the
        // service-to-service HMAC session once so downstream APIs use one contract.
        auth.MapPost(IdentityApiRoutes.SessionExchangeSegment, async (
            HttpContext httpContext,
            BffSessionExchangeRequest? request,
            UserManager<User> userManager,
            IIdentityService identityService,
            IConglomerateTenantRegistry tenantRegistry,
            JwtTokenGenerator tokenGenerator,
            SessionTokenProtector tokenProtector,
            IConnectionMultiplexer redis,
            IUserSessionTracker sessionTracker,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var authResult = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (!authResult.Succeeded || authResult.Principal is null)
                return Results.Unauthorized();

            httpContext.User = authResult.Principal;

            var user = await userManager.GetUserAsync(httpContext.User);
            if (user is null)
                return Results.Unauthorized();

            var existingSessionId = httpContext.Request.Cookies[HisHopeProtocolConstants.Cookies.BrowserSession];
            if (!string.IsNullOrWhiteSpace(existingSessionId))
            {
                // Always rotate the browser BFF session after an OIDC callback. The
                // user may have switched Keycloak accounts while the old cookie still
                // exists; returning early would keep the stale session and omit a new
                // Set-Cookie header.
                await redis.GetDatabase().KeyDeleteAsync($"session:{existingSessionId}");
            }

            var roles = await userManager.GetRolesAsync(user);
            var (permissions, tenantClaims) = await HumanSessionAuthClaims.ResolveAsync(
                userManager,
                identityService,
                user,
                ct);
            if (!string.IsNullOrWhiteSpace(request?.ClientId))
            {
                var clientTenant = tenantRegistry.GetClientTenant(request.ClientId);
                if (clientTenant is null || !tenantRegistry.IsConglomerateClient(request.ClientId))
                    return Results.BadRequest(new { errorCode = "invalid_client", error = "Unknown BFF client." });

                var memberships = tenantClaims
                    .Where(claim => claim.Type == His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.TenantMembership)
                    .Select(claim => claim.Value)
                    .ToArray();
                if (!memberships.Contains(clientTenant, StringComparer.OrdinalIgnoreCase))
                    return Results.Forbid();

                tenantClaims = tenantClaims
                    .Append(new Claim(ConglomerateConstants.ClaimPortalClass, tenantRegistry.GetPortalClass(request.ClientId)))
                    .Append(new Claim(HisHopeProtocolConstants.Claims.TenantClass, tenantRegistry.GetTenantClass(clientTenant)))
                    .ToArray();
            }
            var permissionList = permissions.ToList();
            var sessionAuthMethods = httpContext.User.FindAll(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.AuthenticationMethod)
                .Select(claim => claim.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var sessionAuthTime = httpContext.User.FindFirst("auth_time")?.Value;
            var (jwt, expiresAt) = tokenGenerator.GenerateAccessToken(
                user,
                roles,
                permissionList,
                amrValues: sessionAuthMethods,
                additionalClaims: string.IsNullOrWhiteSpace(sessionAuthTime)
                    ? tenantClaims
                    : tenantClaims.Append(new Claim("auth_time", sessionAuthTime, ClaimValueTypes.Integer64)));
            var sessionId = Guid.NewGuid().ToString("N");
            var csrfToken = Guid.NewGuid().ToString("N");
            var sessionIssuedAt = DateTimeOffset.UtcNow;
            var isPrivileged = configuration.GetSection("Identity:SuperAdmin:UserIds").Get<string[]>()
                ?.Any(id => string.Equals(id, user.Id.ToString(), StringComparison.OrdinalIgnoreCase)) == true;
            var session = new SessionData
            {
                UserId = user.Id.ToString(),
                Jwt = tokenProtector.Protect(jwt),
                RefreshToken = null,
                Permissions = permissionList.ToArray(),
                PrincipalType = AuthorizationConstants.PrincipalTypes.Human,
                CsrfToken = csrfToken,
                UserAgentHash = BffHelpers.ComputeSha256(httpContext.Request.Headers.UserAgent.ToString()),
                IssuedAt = sessionIssuedAt,
                ExpiresAt = expiresAt,
                IsPrivileged = isPrivileged,
                IdleExpiresAt = sessionIssuedAt.Add(isPrivileged ? TimeSpan.FromMinutes(15) : TimeSpan.FromMinutes(30)),
                AbsoluteExpiresAt = sessionIssuedAt.Add(isPrivileged ? TimeSpan.FromHours(4) : TimeSpan.FromHours(8))
            };

            await redis.GetDatabase().StringSetAsync(
                $"session:{sessionId}",
                JsonSerializer.Serialize(session),
                expiresAt - DateTime.UtcNow);
            await sessionTracker.AddSessionAsync(user.Id.ToString(), sessionId);

            httpContext.Response.Cookies.Append(HisHopeProtocolConstants.Cookies.BrowserSession, sessionId, new CookieOptions
            {
                HttpOnly = true,
                Secure = httpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Domain = BffHelpers.CookieDomain(configuration),
                Path = "/",
                MaxAge = expiresAt - DateTime.UtcNow
            });
            httpContext.Response.Cookies.Append("hishop_csrf", csrfToken, new CookieOptions
            {
                HttpOnly = false,
                Secure = httpContext.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Domain = BffHelpers.CookieDomain(configuration),
                Path = "/",
                MaxAge = expiresAt - DateTime.UtcNow
            });

            return Results.NoContent();
        })
        .WithOpenApi()
        .AllowAnonymous();

        auth.MapMethods(IdentityApiRoutes.SessionExchangeSegment, [HttpMethods.Options], () => Results.NoContent())
            .AllowAnonymous()
            .ExcludeFromDescription();

        // BFF internal: exchange session ID for new JWT (transparent refresh)
        auth.MapPost("/internal/refresh", async (IConnectionMultiplexer redis, HttpContext httpContext,
            IIdentityService identityService, SessionTokenProtector tokenProtector,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var guardResult = await His.Hope.IdentityService.Api.Security.BffSessionGuard.ValidateMutatingSessionAsync(
                httpContext, redis, tokenProtector, requireAuthenticatedPrincipal: true);
            if (guardResult is not null)
                return guardResult;

            var sessionId = (string)httpContext.Items["BffSessionId"]!;
            var session = (SessionData)httpContext.Items["BffSession"]!;

            var refreshResult = await identityService.RefreshTokenAsync(
                new RefreshTokenRequest(session.Jwt, session.RefreshToken ?? ""), ct);

            session = session with
            {
                Jwt = tokenProtector.Protect(refreshResult.AccessToken),
                RefreshToken = tokenProtector.Protect(refreshResult.RefreshToken),
                ExpiresAt = refreshResult.ExpiresAt,
                CsrfToken = Guid.NewGuid().ToString("N"),
                UserAgentHash = BffHelpers.ComputeSha256(httpContext.Request.Headers.UserAgent.ToString()),
                IssuedAt = DateTimeOffset.UtcNow
            };

            await redis.GetDatabase().StringSetAsync(
                $"session:{sessionId}",
                JsonSerializer.Serialize(session),
                TimeSpan.FromHours(1));

            httpContext.Response.Cookies.Append(HisHopeProtocolConstants.Cookies.BrowserSession, sessionId, new CookieOptions
            {
                HttpOnly = true,
                Secure = httpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Domain = BffHelpers.CookieDomain(configuration),
                Path = "/",
                MaxAge = TimeSpan.FromHours(1)
            });
            httpContext.Response.Cookies.Append("hishop_csrf", session.CsrfToken, new CookieOptions
            {
                HttpOnly = false,
                Secure = httpContext.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Domain = BffHelpers.CookieDomain(configuration),
                Path = "/",
                MaxAge = TimeSpan.FromHours(1)
            });

            return Results.Ok(new { refreshed = true });
        })
        .WithDeprecationNotice()
        .WithOpenApi()
        .RequireRateLimiting("auth")
        // The session guard performs the authoritative cookie, CSRF, user-agent
        // and principal binding checks. Allow anonymous middleware traversal so
        // a missing cookie returns the contract's 400 response instead of the
        // generic authorization middleware 401.
        .AllowAnonymous();
    }
}
