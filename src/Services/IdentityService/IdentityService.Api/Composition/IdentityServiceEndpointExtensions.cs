using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using His.Hope.ServiceDefaults;
using His.Hope.Persistence;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddictEntityFrameworkCore = OpenIddict.EntityFrameworkCore.Models;
using His.Hope.Bff.Core.Authentication;
using His.Hope.IdentityService.Api.Endpoints;
using His.Hope.IdentityService.Api.Jobs;
using His.Hope.IdentityService.Api.Services;
using His.Hope.IdentityService.Application;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Application.Services;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.IdentityService.Infrastructure.Facility;
using His.Hope.Infrastructure;
using His.Hope.Infrastructure.Audit;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.Contracts;
using His.Hope.Infrastructure.Middleware;
using His.Hope.Infrastructure.Observability;
using His.Hope.Infrastructure.Locking;
using His.Hope.Infrastructure.Security;
using His.Hope.Authorization;
using His.Hope.Authorization.Handlers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using Serilog;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Composition;

public static class IdentityServiceEndpointExtensions
{
    public static void MapIdentityServiceEndpoints(this WebApplication app)
    {
app.MapGet("/api/v1/localization", async (
    string[]? key,
    string? locale,
    HttpContext httpContext,
    IdentityDbContext db,
    CancellationToken ct) =>
{
    var requestedLocale = NormalizeLocale(locale ?? httpContext.Request.Headers["Accept-Language"].ToString());
    var keys = (key ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToArray();
    var query = db.LocalizationTranslations.AsNoTracking()
        .Where(translation => translation.Locale == requestedLocale || translation.Locale == "vi-VN");
    if (keys.Length > 0) query = query.Where(translation => keys.Contains(translation.ResourceKey));

    var translations = await query.ToListAsync(ct);
    var values = translations
        .GroupBy(translation => translation.ResourceKey, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group
            .OrderByDescending(translation => translation.Locale.Equals(requestedLocale, StringComparison.OrdinalIgnoreCase))
            .Select(translation => translation.Value)
            .First(), StringComparer.OrdinalIgnoreCase);

    return Results.Ok(new { locale = requestedLocale, fallbackLocale = "vi-VN", values });
}).AllowAnonymous();

var auth = app.MapGroup("/api/v1/auth");

auth.MapPost("/login", async (LoginRequest request, IIdentityService identityService,
    UserManager<User> userManager, SignInManager<User> signInManager,
    IConnectionMultiplexer redis, SessionTokenProtector tokenProtector,
    IConfiguration configuration, HttpContext httpContext, CancellationToken ct) =>
{
    try
    {
        var result = await identityService.LoginAsync(request, ct);

        // Keep the browser SSO cookie aligned with the BFF session. The BFF
        // cookie carries the server-side token, while the Identity cookie is
        // used by browser-only endpoints such as session-status and admin UI.
        var identityUser = await userManager.FindByIdAsync(result.User.Id.ToString());
        if (identityUser is null)
            return Results.Unauthorized();

        // The legacy JSON login endpoint must not bypass the OIDC MFA gate.
        // Browser and mobile clients use /connect/authorize, but older API
        // consumers can still call this endpoint directly.
        if (identityUser.TwoFactorEnabled)
        {
            return Results.Json(new
            {
                error = "mfa_required",
                errorDescription = "MFA verification is required before a session can be issued.",
                requiresMfa = true
            }, statusCode: StatusCodes.Status401Unauthorized);
        }

        // Keep the identity cookie on the same permission contract as JWT.
        // Permission policies must behave identically for legacy browser login,
        // BFF sessions, and OIDC bearer tokens.
        var roles = await userManager.GetRolesAsync(identityUser);
        var permissions = RolePermissionMapping.GetPermissionsForRoles(roles).ToArray();
        var identityPrincipal = await signInManager.CreateUserPrincipalAsync(identityUser);
        if (identityPrincipal.Identity is ClaimsIdentity identityClaims)
        {
            foreach (var permission in permissions)
                identityClaims.AddClaim(new Claim("permissions", permission));
        }
        await httpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            identityPrincipal,
            new AuthenticationProperties { IsPersistent = true });

        // BFF: Create Redis session and set cookies (dual-mode: cookie + Bearer)
        var sessionId = Guid.NewGuid().ToString("N");
        var csrfToken = Guid.NewGuid().ToString("N");
        var sessionData = new SessionData
        {
            UserId = result.User.Id.ToString(),
            Jwt = tokenProtector.Protect(result.AccessToken),
            RefreshToken = tokenProtector.Protect(result.RefreshToken),
            Permissions = permissions,
            CsrfToken = csrfToken,
            UserAgentHash = BffHelpers.ComputeSha256(httpContext.Request.Headers.UserAgent.ToString()),
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = result.ExpiresAt
        };

        var db = redis.GetDatabase();
        await db.StringSetAsync(
            $"session:{sessionId}",
            JsonSerializer.Serialize(sessionData),
            TimeSpan.FromHours(1));

        httpContext.Response.Cookies.Append("hishop_sid", sessionId, new CookieOptions
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Domain = BffHelpers.CookieDomain(configuration),
            Path = "/",
            MaxAge = TimeSpan.FromHours(1)
        });

        httpContext.Response.Cookies.Append("hishop_csrf", csrfToken, new CookieOptions
        {
            HttpOnly = false,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Domain = BffHelpers.CookieDomain(configuration),
            Path = "/",
            MaxAge = TimeSpan.FromHours(1)
        });

        // SECURITY: BFF mode — return session confirmation only, not tokens.
        // Tokens are stored HttpOnly via cookie. Client uses /internal/refresh for token ops.
        return Results.Ok(new
        {
            status = "ok",
            userId = result.User.Id,
            requiresMfa = false
        });
    }

    catch (UnauthorizedAccessException ex)
    {
        return Results.Problem(ex.Message, statusCode: 401);
    }
})
.WithDeprecationNotice()
.WithOpenApi()
.RequireRateLimiting("auth")
.AllowAnonymous();

auth.MapPost("/ldap/login", async (LdapLoginRequest request, LdapSyncService ldap,
    OidcLoginCompletionService completion, HttpContext context, CancellationToken ct) =>
{
    var profile = await ldap.AuthenticateAndGetProfileAsync(request.UserName, request.Password, ct);
    if (profile is null || !profile.IsActive)
        return Results.Unauthorized();

    var user = await ldap.ProvisionUserAsync(profile, ct);
    if (!user.IsActive)
        return Results.Unauthorized();

    var completed = await completion.CompletePrimaryAsync(context, user, "/", ["ldap"], ct);
    return Results.Ok(new { authenticated = !completed.RequiresMfa, requiresMfa = completed.RequiresMfa, mfaUrl = completed.RedirectUrl, userId = user.Id });
})
.WithOpenApi()
.RequireRateLimiting("auth")
.AllowAnonymous();

auth.MapPost("/register", async (RegisterRequest request, IIdentityService identityService, CancellationToken ct) =>
{
    try
    {
        var result = await identityService.RegisterAsync(request, ct);
        return Results.Created("/api/v1/auth/me", result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
})
.WithOpenApi()
.AllowAnonymous();

auth.MapPost("/refresh", async (RefreshTokenRequest request, IIdentityService identityService,
    IConnectionMultiplexer redis, SessionTokenProtector tokenProtector,
    HttpContext httpContext, ILogger<Program> logger, CancellationToken ct) =>
{
    try
    {
        var sessionId = httpContext.Request.Cookies["hishop_sid"];
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
    catch (UnauthorizedAccessException ex)
    {
        return Results.Problem(ex.Message, statusCode: 401);
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
    var sessionId = httpContext.Request.Cookies["hishop_sid"];
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
                ?? httpContext.User.FindFirstValue("sub");
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
    httpContext.Response.Cookies.Append("hishop_sid", "", new CookieOptions
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
        HttpOnly = false, Secure = httpContext.Request.IsHttps, SameSite = SameSiteMode.Strict,
        Domain = BffHelpers.CookieDomain(configuration),
        Path = "/", Expires = DateTimeOffset.UnixEpoch
    });

    return Results.NoContent();
})
.WithDeprecationNotice()
.WithOpenApi()
.RequireRateLimiting("auth")
.AllowAnonymous();

auth.MapGet("/session-status", async (HttpContext httpContext) =>
{
    var result = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
    return Results.Ok(new
    {
        authenticated = result.Succeeded,
        userName = result.Principal?.Identity?.Name
    });
})
.WithOpenApi()
.AllowAnonymous();

// SPA OIDC callback -> BFF session bridge. The browser may have a valid
// OpenIddict access token without the legacy hishop_sid cookie; mint the
// service-to-service HMAC session once so downstream APIs use one contract.
auth.MapPost("/session/exchange", async (
    HttpContext httpContext,
    UserManager<User> userManager,
    IdentityDbContext db,
    JwtTokenGenerator tokenGenerator,
    SessionTokenProtector tokenProtector,
    IConnectionMultiplexer redis,
    IConfiguration configuration,
    CancellationToken ct) =>
{
    if (httpContext.User.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();

    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
        return Results.Unauthorized();

    var existingSessionId = httpContext.Request.Cookies["hishop_sid"];
    if (!string.IsNullOrWhiteSpace(existingSessionId))
    {
        // Always rotate the browser BFF session after an OIDC callback. The
        // user may have switched Keycloak accounts while the old cookie still
        // exists; returning early would keep the stale session and omit a new
        // Set-Cookie header.
        await redis.GetDatabase().KeyDeleteAsync($"session:{existingSessionId}");
    }

    var roles = await userManager.GetRolesAsync(user);
    var roleIds = await db.Roles
        .Where(role => roles.Contains(role.Name!))
        .Select(role => role.Id)
        .ToArrayAsync(ct);
    var permissions = await db.RolePermissions
        .Where(rolePermission => roleIds.Contains(rolePermission.RoleId))
        .Select(rolePermission => rolePermission.PermissionCode)
        .ToArrayAsync(ct);
    permissions = permissions
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    var (jwt, expiresAt) = tokenGenerator.GenerateAccessToken(user, roles, permissions);
    var sessionId = Guid.NewGuid().ToString("N");
    var csrfToken = Guid.NewGuid().ToString("N");
    var session = new SessionData
    {
        UserId = user.Id.ToString(),
        Jwt = tokenProtector.Protect(jwt),
        RefreshToken = null,
        Permissions = permissions,
        CsrfToken = csrfToken,
        UserAgentHash = BffHelpers.ComputeSha256(httpContext.Request.Headers.UserAgent.ToString()),
        IssuedAt = DateTimeOffset.UtcNow,
        ExpiresAt = expiresAt
    };

    await redis.GetDatabase().StringSetAsync(
        $"session:{sessionId}",
        JsonSerializer.Serialize(session),
        expiresAt - DateTime.UtcNow);

    httpContext.Response.Cookies.Append("hishop_sid", sessionId, new CookieOptions
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
.RequireAuthorization();

// BFF internal: exchange session ID for new JWT (transparent refresh)
auth.MapPost("/internal/refresh", async (IConnectionMultiplexer redis, HttpContext httpContext,
    IIdentityService identityService, SessionTokenProtector tokenProtector,
    IConfiguration configuration,
    CancellationToken ct) =>
{
    var sessionId = httpContext.Request.Cookies["hishop_sid"];
    if (string.IsNullOrEmpty(sessionId))
        return Results.BadRequest(new { error = "No session cookie" });

    var db = redis.GetDatabase();
    var sessionJson = await db.StringGetAsync($"session:{sessionId}");
    if (!sessionJson.HasValue)
        return Results.Unauthorized();

    SessionData? session;
    try
    {
        session = JsonSerializer.Deserialize<SessionData>(sessionJson!);
        if (session is not null)
        {
            session = session with
            {
                Jwt = tokenProtector.Unprotect(session.Jwt),
                RefreshToken = tokenProtector.UnprotectOptional(session.RefreshToken)
            };
        }
    }
    catch (System.Security.Cryptography.CryptographicException)
    {
        return Results.Unauthorized();
    }
    if (session is null || session.IsExpired)
        return Results.Unauthorized();

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

    await db.StringSetAsync(
        $"session:{sessionId}",
        JsonSerializer.Serialize(session),
        TimeSpan.FromHours(1));

    httpContext.Response.Cookies.Append("hishop_sid", sessionId, new CookieOptions
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
        HttpOnly = false, Secure = httpContext.Request.IsHttps, SameSite = SameSiteMode.Strict,
        Domain = BffHelpers.CookieDomain(configuration),
        Path = "/", MaxAge = TimeSpan.FromHours(1)
    });

    return Results.Ok(new { refreshed = true });
})
.WithDeprecationNotice()
.WithOpenApi()
.AllowAnonymous();

auth.MapGet("/verify", async (HttpContext httpContext) =>
{
    if (httpContext.User.Identity?.IsAuthenticated == true)
        return Results.Ok(new { authenticated = true });
    return Results.Ok(new { authenticated = false });
})
.WithOpenApi()
.AllowAnonymous();

auth.MapGet("/me", async (HttpContext httpContext, IIdentityService identityService, CancellationToken ct) =>
{
    var userIdClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
        ?? httpContext.User.FindFirst("sub");
    if (userIdClaim is null) return Results.Unauthorized();
    var userId = Guid.Parse(userIdClaim.Value);
    var user = await identityService.GetUserByIdAsync(userId, ct);
    return Results.Ok(user);
})
.RequireAuthorization()
.WithOpenApi();

auth.MapPost("/check-permission", (PermissionCheckRequest request, HttpContext httpContext) =>
{
    var permission = request.Permission?.Trim();
    if (string.IsNullOrWhiteSpace(permission))
        return Results.BadRequest(new { error = "Permission is required" });

    var granted = httpContext.User
        .FindAll("permissions")
        .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Contains(permission, StringComparer.OrdinalIgnoreCase);

    return Results.Ok(new { granted });
})
.RequireAuthorization()
.WithOpenApi();

// External login challenge endpoint
auth.MapGet("/external-login/{provider}", (string provider, HttpContext httpContext) =>
{
    var redirectUrl = $"/api/v1/auth/external-callback/{provider}";
    var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
    properties.Items["LoginProvider"] = provider;
    return Results.Challenge(properties, new[] { provider });
})
.AllowAnonymous();

// External login callback (OIDC redirect handler)
auth.MapGet("/external-callback/{provider}", async (
    string provider, HttpContext httpContext,
    UserManager<User> userManager,
    OidcLoginCompletionService completion, CancellationToken ct) =>
{
    var result = await httpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
    if (!result.Succeeded)
        return Results.Redirect("/login?error=external_failed");

    var externalPrincipal = result.Principal;
    var email = externalPrincipal.FindFirstValue(ClaimTypes.Email);
    var name = externalPrincipal.FindFirstValue(ClaimTypes.Name);
    var providerKey = externalPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(email))
        return Results.Redirect("/login?error=no_email");

    var user = await userManager.FindByEmailAsync(email);

    if (user is null)
    {
        user = new User
        {
            UserName = email,
            Email = email,
            FirstName = name?.Split(' ').FirstOrDefault() ?? email,
            LastName = name?.Split(' ').Skip(1).LastOrDefault() ?? "",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
            return Results.Redirect("/login?error=registration_failed");

        await userManager.AddToRoleAsync(user, "Provider");
    }

    var existingLogins = await userManager.GetLoginsAsync(user);
    if (!existingLogins.Any(l => l.LoginProvider == provider && l.ProviderKey == providerKey))
    {
        await userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerKey!, provider));
    }

    var returnUrl = httpContext.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
    var completed = await completion.CompletePrimaryAsync(httpContext, user, returnUrl, [provider], ct);
    return Results.Redirect(completed.RedirectUrl);
})
.AllowAnonymous();

// List available external login providers
auth.MapGet("/external-providers", (IConfiguration config) =>
{
    var providers = new List<object>();

    if (!string.IsNullOrEmpty(config["Authentication:Google:ClientId"]))
        providers.Add(new { provider = "Google", displayName = "Google", icon = "google" });

    if (!string.IsNullOrEmpty(config["Authentication:Microsoft:ClientId"]))
        providers.Add(new { provider = "Microsoft", displayName = "Microsoft", icon = "microsoft" });

    return Results.Ok(new { providers });
})
.AllowAnonymous();

// MFA endpoints
auth.MapMfaEndpoints();

// Account linking endpoints
auth.MapGroup("/account").MapAccountLinkingEndpoints();

// SECURITY: Token revocation endpoints
auth.MapTokenRevocationEndpoints();

// User consent management
auth.MapGroup("/consents").MapConsentEndpoints();

// User management endpoints
var secured = app.MapGroup("/api/v1/auth").RequireAuthorization();

secured.MapPut("/me/preferences", async (
    UpdateLanguagePreferenceRequest request,
    UserManager<User> userManager,
    ClaimsPrincipal principal) =>
{
    var locale = request.PreferredLanguage?.Trim();
    if (locale is not ("vi-VN" or "en-US" or "en"))
        return Results.BadRequest(new { error = "unsupported_locale", supported = new[] { "vi-VN", "en-US" } });

    var user = await userManager.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();
    user.PreferredLanguage = locale.Equals("en", StringComparison.OrdinalIgnoreCase) ? "en-US" : locale;
    var result = await userManager.UpdateAsync(user);
    return result.Succeeded
        ? Results.Ok(new { preferredLanguage = user.PreferredLanguage })
        : Results.Problem("Unable to save language preference.", statusCode: StatusCodes.Status500InternalServerError);
});
secured.MapUserEndpoints();
secured.MapRoleEndpoints();

// Admin API endpoints (for frontend admin module)
var admin = app.MapGroup("/api/v1/admin").RequireAuthorization();
admin.MapUserEndpoints();
admin.MapRoleEndpoints();
admin.MapSettingsEndpoints();
admin.MapAuditLogEndpoints();
admin.MapGroup("/clients").MapClientEndpoints();
ClientEndpoints.MapDynamicClientRegistration(app);
admin.MapBulkImportEndpoints();
admin.MapAdminTableEndpoints();
admin.MapTableViewEndpoints();
        admin.MapTableAnalysisEndpoints();

        app.MapMobilePlatformEndpoints();
        app.MapGet("/api/v1/auth/identity-login.js", (HttpContext context) =>
        {
            var scriptPath = ResolveIdentityLoginScriptPath(app.Environment);
            if (!File.Exists(scriptPath))
                return Results.NotFound();

            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
            return Results.File(scriptPath, "text/javascript; charset=utf-8");
        })
            .AllowAnonymous();
        app.MapPasskeyEndpoints();
admin.MapGet("/me/permissions", async (HttpContext httpContext, UserManager<User> userManager) =>
{
    var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? httpContext.User.FindFirstValue("sub");
    var user = Guid.TryParse(userId, out var parsedUserId)
        ? await userManager.FindByIdAsync(parsedUserId.ToString())
        : null;

    if (user is not null)
    {
        var roles = await userManager.GetRolesAsync(user);
        return Results.Ok(new
        {
            userId,
            userName = user.Email ?? user.UserName,
            roles = roles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            permissions = RolePermissionMapping.GetPermissionsForRoles(roles)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        });
    }

    return Results.Ok(new
    {
        userId,
        userName = httpContext.User.Identity?.Name,
        roles = httpContext.User.FindAll(ClaimTypes.Role).Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
        permissions = httpContext.User.FindAll("permission").Concat(httpContext.User.FindAll("permissions"))
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
    });
}).RequireAuthorization();
admin.MapGroup("/consents").RequireAuthorization("Permission:admin.users.read").MapGet("/", async (
    int page = 1,
    int pageSize = 20,
    string? search = null,
    string? clientId = null,
    string? sort = null,
    IdentityDbContext db = null!,
    CancellationToken ct = default) =>
{
    if (page < 1 || pageSize is < 1 or > 100)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["pageSize"] = ["pageSize must be between 1 and 100 and page must be at least 1."] });
    if (search?.Length > 100 || clientId?.Length > 100 || sort?.Length > 100)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["search"] = ["Search must be 100 characters or fewer."] });

    var query = db.ClientConsents.AsNoTracking().Where(c => c.IsActive);
    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim();
        query = query.Where(c => c.ClientId.Contains(term));
    }
    if (!string.IsNullOrWhiteSpace(clientId))
        query = query.Where(c => c.ClientId.Contains(clientId.Trim()));

    var sortParts = sort?.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    var descending = sortParts?.Length > 1 && string.Equals(sortParts[1], "desc", StringComparison.OrdinalIgnoreCase);
    query = (sortParts?.FirstOrDefault()?.ToLowerInvariant(), descending) switch
    {
        ("clientid", false) => query.OrderBy(c => c.ClientId),
        ("clientid", true) => query.OrderByDescending(c => c.ClientId),
        ("created", false) => query.OrderBy(c => c.GrantedAt),
        _ => query.OrderByDescending(c => c.GrantedAt)
    };

    var totalCount = await query.CountAsync(ct);
    var consents = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(ct);

    var userIds = consents.Select(c => c.UserId).Distinct().ToArray();
    var users = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Email ?? u.Id.ToString(), ct);
    var items = consents.Select(c => new
    {
        id = c.Id,
        subject = users.GetValueOrDefault(c.UserId, c.UserId.ToString()),
        clientId = c.ClientId,
        scopes = JsonSerializer.Deserialize<List<string>>(c.Scopes) ?? new List<string>(),
        created = c.GrantedAt,
        expiresAt = c.ExpiresAt
    }).ToList();
    return Results.Ok(new PagedResult<object>(items, totalCount, page, pageSize));
});

admin.MapGet("/dashboard", async (IdentityDbContext db, CancellationToken ct) =>
{
    var totalUsers = await db.Users.CountAsync(ct);
    var activeUsers = await db.Users.CountAsync(u => u.IsActive, ct);
    var totalRoles = await db.Roles.CountAsync(ct);
    var totalClients = await db.Set<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreApplication>().CountAsync(ct);
    var activeConsents = await db.ClientConsents.CountAsync(c => c.IsActive, ct);
    return Results.Ok(new { totalUsers, activeUsers, totalRoles, totalClients, activeConsents });
}).RequireAuthorization("Permission:admin.users.read");

// Manual LDAP sync trigger
admin.MapPost("/ldap/sync", async (LdapSyncService syncService, CancellationToken ct) =>
{
    await syncService.SyncAsync(ct);
    return Results.Ok(new { message = "LDAP sync completed" });
}).RequireAuthorization("Permission:admin.users.read");

// Key rotation (admin only)
admin.MapPost("/security/rotate-signing-key", async (VaultKeyService keyService, CancellationToken ct) =>
{
    await keyService.RotateKeyAsync(ct);
    return Results.Ok(new { message = "Signing key rotated successfully" });
}).RequireAuthorization("Permission:admin.users.read");

var settings = app.MapGroup("/api/v1").RequireAuthorization();
settings.MapSettingsEndpoints();

var audit = app.MapGroup("/api/v1").RequireAuthorization();
audit.MapAuditLogEndpoints();

// HR webhook (requires API key - validated via middleware or API key header)
var webhook = app.MapGroup("/api/v1");
webhook.MapHrWebhookEndpoints();

// Frontend runtime error reports are best-effort telemetry. Keep the endpoint
// available so a failed report never creates a secondary 404 in the client.
app.MapPost("/api/v1/errors", (HttpContext context, ILogger<Program> logger) =>
{
    logger.LogWarning("Frontend error report received. CorrelationId={CorrelationId}",
        context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? context.TraceIdentifier);
    return Results.NoContent();
}).AllowAnonymous();


// gRPC endpoints
app.MapGrpcService<His.Hope.IdentityService.Api.Services.GrpcIdentityService>();

if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
}

// ─── OIDC Discovery: JWKS endpoint ───
app.MapGet("/.well-known/jwks", async (IVaultKeyProvider vaultKeyProvider, CancellationToken ct) =>
{
    var jwks = await vaultKeyProvider.GetJwksAsync(ct);
    return Results.Ok(new { keys = jwks });
})
.AllowAnonymous();

// Service-to-service consumers run inside the Docker network and cannot use
// the browser-facing localhost JWKS URL from the public discovery document.
app.MapGet("/.well-known/internal-openid-configuration", (IConfiguration configuration) =>
{
    var issuer = configuration["OpenIddict:Issuer"]?.TrimEnd('/') + "/";
    var jwksUri = configuration["OpenIddict:InternalJwksUri"]
        ?? "http://identityservice:5001/.well-known/jwks";

    return Results.Ok(new
    {
        issuer,
        jwks_uri = jwksUri,
        id_token_signing_alg_values_supported = new[] { "RS256" }
    });
})
.AllowAnonymous();

// ─── SCIM v2 Provisioning API (RFC 7643/7644) ───
app.MapScimEndpoints();

// ─── OIDC Authorization Endpoint (passthrough handler) ───
// OpenIddict validates the request and passes through. When the user is
// authenticated (cookie), we sign in with the OpenIddict scheme to generate
// the authorization code and redirect to the callback.
app.MapGet("/connect/authorize", async (
    HttpContext context,
    SignInManager<User> signInManager,
    UserManager<User> userManager,
    IOpenIddictScopeManager scopeManager,
    IdentityDbContext db,
    OidcLoginCompletionService completion) =>
{
    // Access the OpenIddict server transaction to get the validated request
    var feature = context.Features.Get<OpenIddictServerAspNetCoreFeature>();
    var request = feature?.Transaction?.Request
        ?? throw new InvalidOperationException("OpenIddict request not found.");

    // User must be authenticated (cookie from earlier login)
    if (context.User.Identity is not { IsAuthenticated: true })
    {
        return Results.Challenge(new AuthenticationProperties
        {
            RedirectUri = context.Request.Path + context.Request.QueryString
        }, new[] { IdentityConstants.ApplicationScheme });
    }

    var user = await userManager.GetUserAsync(context.User)
        ?? throw new InvalidOperationException("Authenticated user not found.");

    // A valid application cookie is not proof that the current OIDC
    // authorization transaction completed MFA. Re-enter the shared MFA gate
    // unless the cookie carries a verified second-factor amr claim.
    var mfaEnabled = user.TwoFactorEnabled || await db.UserMfas
        .AsNoTracking()
        .AnyAsync(item => item.UserId == user.Id && item.IsEnabled, context.RequestAborted);
    if (mfaEnabled && !HasCompletedMfa(context.User))
    {
        var authorizeReturnUrl = context.Request.Path + context.Request.QueryString;
        var authenticationMethods = context.User.FindAll("amr")
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (authenticationMethods.Length == 0)
            authenticationMethods = ["pwd"];

        var mfa = await completion.CompletePrimaryAsync(
            context,
            user,
            authorizeReturnUrl,
            authenticationMethods,
            context.RequestAborted);
        return Results.Redirect(mfa.RedirectUrl);
    }

    var requestedScopes = request.GetScopes().ToHashSet(StringComparer.OrdinalIgnoreCase);
    var existingConsent = await db.ClientConsents
        .AsNoTracking()
        .FirstOrDefaultAsync(c => c.UserId == user.Id && c.ClientId == request.ClientId && c.IsActive, context.RequestAborted);
    var grantedScopes = existingConsent is null
        ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        : ParseConsentScopes(existingConsent.Scopes);

    if (!requestedScopes.IsSubsetOf(grantedScopes))
    {
        var consentReturnUrl = context.Request.Path + context.Request.QueryString;
        return Results.Redirect($"/Account/Consent?returnUrl={Uri.EscapeDataString(consentReturnUrl)}");
    }

    var principal = await signInManager.CreateUserPrincipalAsync(user);

    // Ensure sub claim is set (OpenIddict requires it on the principal directly)
    principal.SetClaim(OpenIddictConstants.Claims.Subject, user.Id.ToString());

    // Preserve the authentication methods completed in the interactive
    // cookie. CreateUserPrincipalAsync builds a fresh principal from the
    // user record and otherwise drops amr=otp/passkey, causing Angular/mobile
    // MFA status checks to reject a session that already passed MFA.
    var completedAuthenticationMethods = context.User.FindAll("amr")
        .Select(claim => claim.Value)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    if (principal.Identity is ClaimsIdentity principalIdentity)
    {
        foreach (var method in completedAuthenticationMethods)
        {
            if (!principalIdentity.FindAll("amr").Any(claim =>
                    string.Equals(claim.Value, method, StringComparison.OrdinalIgnoreCase)))
            {
                principalIdentity.AddClaim(new Claim("amr", method));
            }
        }

        // Keep the verified second factor explicit and scalar in the OIDC
        // token. This avoids claim serializers collapsing multiple amr values
        // into a shape that the Angular/mobile status endpoint cannot read.
        var verifiedSecondFactor = completedAuthenticationMethods.FirstOrDefault(
            method => string.Equals(method, "passkey", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(method, "otp", StringComparison.OrdinalIgnoreCase));
        if (verifiedSecondFactor is not null)
            principal.SetClaim("amr", verifiedSecondFactor);
    }

    principal.SetScopes(request.GetScopes());

    // Permissions are derived from the persisted roles at authorization time
    // so every newly issued access token carries the same policy data used by
    // the admin APIs. This is intentionally done before SignIn: token-request
    // handlers are not guaranteed to run for every authorization-code flow.
    var roleNames = (await userManager.GetRolesAsync(user))
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    if (principal.Identity is ClaimsIdentity roleIdentity)
    {
        foreach (var roleName in roleNames)
        {
            if (!roleIdentity.FindAll(OpenIddictConstants.Claims.Role)
                .Any(claim => string.Equals(claim.Value, roleName, StringComparison.OrdinalIgnoreCase)))
            {
                roleIdentity.AddClaim(new Claim(OpenIddictConstants.Claims.Role, roleName));
            }
        }
    }
    var permissions = await db.RolePermissions
        .Where(rolePermission => roleNames.Contains(rolePermission.Role.Name!))
        .Select(rolePermission => rolePermission.PermissionCode)
        .Distinct()
        .ToListAsync(context.RequestAborted);
    if (permissions.Count > 0)
        principal.SetClaim("permissions", string.Join(",", permissions));

    var resources = new List<string>();
    await foreach (var resource in scopeManager.ListResourcesAsync(principal.GetScopes()))
        resources.Add(resource);
    principal.SetResources(resources);

    // Set claim destinations: required for OpenIddict to accept the principal
    foreach (var claim in principal.Claims)
    {
        claim.SetDestinations(claim.Type switch
        {
            // Identity claims go to access + identity token
            "name" or "given_name" or "family_name" or "email" => new[] {
                OpenIddictConstants.Destinations.AccessToken,
                OpenIddictConstants.Destinations.IdentityToken },
            // Role claim goes to access token
            "role" or ClaimTypes.Role => new[] {
                OpenIddictConstants.Destinations.AccessToken },
            _ => new[] { OpenIddictConstants.Destinations.AccessToken }
        });
    }

    return Results.SignIn(principal,
        properties: new AuthenticationProperties { RedirectUri = request.RedirectUri },
        authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
});

// ─── OIDC Consent Page ───────────────────────────────────────────────
// The authorization request has already been validated by OpenIddict when
// this page is reached. The protected request token prevents form tampering
// with redirect_uri, client_id, or scopes while the user is deciding.
app.MapGet("/Account/Consent", async (
    HttpContext context,
    IdentityDbContext db,
    IDataProtectionProvider dataProtectionProvider,
    string returnUrl) =>
{
    if (context.User.Identity?.IsAuthenticated != true)
    {
        var loginReturnUrl = $"/Account/Consent?returnUrl={Uri.EscapeDataString(returnUrl)}";
        return Results.Redirect($"/Account/Login?ReturnUrl={Uri.EscapeDataString(loginReturnUrl)}");
    }

    if (!TryReadAuthorizeRequest(returnUrl, out var clientId, out var redirectUri, out var scopes, out var state))
        return Results.BadRequest("Invalid authorization request.");

    var application = await db.OpenIddictApplications.AsNoTracking()
        .FirstOrDefaultAsync(a => a.ClientId == clientId, context.RequestAborted);
    if (application is null)
        return Results.BadRequest("Unknown client application.");

    var redirectUris = ParseStringList(application.RedirectUris);
    if (!redirectUris.Contains(redirectUri, StringComparer.Ordinal))
        return Results.BadRequest("Invalid client redirect URI.");

    var protector = dataProtectionProvider.CreateProtector("HisHope.OidcConsent.v1");
    var protectedRequest = protector.Protect(returnUrl);
    var html = BuildConsentPage(
        application.DisplayName ?? clientId,
        clientId,
        redirectUri,
        scopes,
        protectedRequest,
        state);

    context.Response.OnStarting(() =>
    {
        context.Response.Headers.Remove("Content-Security-Policy");
        return Task.CompletedTask;
    });
    return Results.Content(html, "text/html; charset=utf-8");
})
.AllowAnonymous();

app.MapPost("/Account/Consent", async (
    HttpContext context,
    IdentityDbContext db,
    IDataProtectionProvider dataProtectionProvider,
    His.Hope.Infrastructure.Audit.IAuditService auditService) =>
{
    var form = await context.Request.ReadFormAsync(context.RequestAborted);
    var protectedRequest = form["request"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(protectedRequest))
        return Results.BadRequest("Missing consent request.");

    string returnUrl;
    try
    {
        var protector = dataProtectionProvider.CreateProtector("HisHope.OidcConsent.v1");
        returnUrl = protector.Unprotect(protectedRequest);
    }
    catch (Exception) when (context.RequestAborted.IsCancellationRequested == false)
    {
        return Results.BadRequest("Expired consent request.");
    }

    if (!TryReadAuthorizeRequest(returnUrl, out var clientId, out var redirectUri, out var scopes, out _))
        return Results.BadRequest("Invalid consent request.");

    var application = await db.OpenIddictApplications.AsNoTracking()
        .FirstOrDefaultAsync(a => a.ClientId == clientId, context.RequestAborted);
    if (application is null || !ParseStringList(application.RedirectUris).Contains(redirectUri, StringComparer.Ordinal))
        return Results.BadRequest("Invalid client redirect URI.");

    if (context.User.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();

    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? context.User.FindFirstValue("sub")
        ?? "unknown";
    var decision = string.Equals(form["decision"].FirstOrDefault(), "allow", StringComparison.OrdinalIgnoreCase)
        ? "CONSENT_GRANTED"
        : "CONSENT_DENIED";

    if (!string.Equals(form["decision"].FirstOrDefault(), "allow", StringComparison.OrdinalIgnoreCase))
    {
        await auditService.LogPhiAccessAsync(new His.Hope.Infrastructure.Audit.PhiAuditEntry
        {
            UserId = userId,
            ResourceType = "OidcClient",
            ResourceId = clientId,
            Action = decision,
            ClientIp = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            CorrelationId = context.TraceIdentifier,
            HttpMethod = context.Request.Method,
            Path = context.Request.Path
        }, context.RequestAborted);
        return Results.Redirect(BuildAuthorizationErrorRedirect(returnUrl, "access_denied", "The user denied access."));
    }

    if (!Guid.TryParse(userId, out var userGuid))
        return Results.Unauthorized();

    var consent = await db.ClientConsents
        .FirstOrDefaultAsync(c => c.UserId == userGuid && c.ClientId == clientId, context.RequestAborted);
    if (consent is null)
    {
        consent = new ClientConsent { UserId = userGuid, ClientId = clientId };
        db.ClientConsents.Add(consent);
    }

    consent.Scopes = JsonSerializer.Serialize(scopes);
    consent.GrantedAt = DateTime.UtcNow;
    consent.IsActive = true;
    consent.RevokedAt = null;
    await db.SaveChangesAsync(context.RequestAborted);

    await auditService.LogPhiAccessAsync(new His.Hope.Infrastructure.Audit.PhiAuditEntry
    {
        UserId = userId,
        ResourceType = "OidcClient",
        ResourceId = clientId,
        Action = decision,
        ClientIp = context.Connection.RemoteIpAddress?.ToString(),
        UserAgent = context.Request.Headers.UserAgent.ToString(),
        CorrelationId = context.TraceIdentifier,
        HttpMethod = context.Request.Method,
        Path = context.Request.Path
    }, context.RequestAborted);

    return Results.Redirect(returnUrl);
})
.AllowAnonymous();

// ─── OIDC Login Page (server-rendered for authorization flow) ───
app.MapGet("/Account/Login", async (HttpContext httpContext, SignInManager<User> signInManager,
    ExternalIdentityProviderRuntime externalIdentityRuntime) =>
{
    var returnUrl = httpContext.Request.Query["returnUrl"].FirstOrDefault() ?? "/";

    // If user is already authenticated, show already-signed-in page
    if (httpContext.User.Identity?.IsAuthenticated == true)
    {
        var userName = httpContext.User.Identity.Name ?? "User";
        var pageHtml = BuildAlreadySignedInPage(userName, returnUrl);
        httpContext.Response.OnStarting(() =>
        {
            httpContext.Response.Headers.Remove("Content-Security-Policy");
            return Task.CompletedTask;
        });
        return Results.Content(pageHtml, "text/html; charset=utf-8");
    }

    var externalSchemes = await signInManager.GetExternalAuthenticationSchemesAsync();
    var externalProviders = externalSchemes
        .Select(s => s.Name)
        .Where(n => n != "HisHope.BrowserOrApi") // exclude internal forwarding scheme
        .ToList();

    var error = httpContext.Request.Query["error"].FirstOrDefault();
    var errorMessage = error switch
    {
        "invalid_credentials" => "Invalid email or password.",
        "invalid_directory_credentials" => "Invalid hospital directory username or password.",
        "directory_unavailable" => "Hospital directory sign-in is not configured in this environment.",
        _ => error ?? ""
    };
    var hasError = !string.IsNullOrEmpty(error);
    var encodedReturnUrl = System.Net.WebUtility.HtmlEncode(returnUrl);

    var samlSettings = await externalIdentityRuntime.GetSamlAsync(httpContext.RequestAborted);
    var samlAvailable = samlSettings.Enabled && !string.IsNullOrWhiteSpace(samlSettings.IdpMetadata);
    var html = BuildLoginPage(hasError, errorMessage, encodedReturnUrl, externalProviders, samlAvailable);

    // Remove restrictive CSP on response flush — login page CSS is self-contained (SVG, no external fonts)
    httpContext.Response.OnStarting(() =>
    {
        httpContext.Response.Headers.Remove("Content-Security-Policy");
        httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        httpContext.Response.Headers.Pragma = "no-cache";
        return Task.CompletedTask;
    });

    return Results.Content(html, "text/html; charset=utf-8");
})
.AllowAnonymous();

app.MapGet("/Account/Mfa", async (HttpContext httpContext, string? error, OidcLoginCompletionService completion,
    CancellationToken ct) =>
{
    var methods = await completion.GetPendingMfaMethodsAsync(httpContext, ct);
    var html = BuildMfaPage(error, methods);

    httpContext.Response.OnStarting(() =>
    {
        httpContext.Response.Headers.Remove("Content-Security-Policy");
        httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        httpContext.Response.Headers.Pragma = "no-cache";
        return Task.CompletedTask;
    });

    return Results.Content(html, "text/html; charset=utf-8");
})
    .AllowAnonymous();

app.MapPost("/Account/Mfa", async (HttpContext context, OidcLoginCompletionService completion,
    CancellationToken ct) =>
{
    var form = await context.Request.ReadFormAsync(ct);
    var redirectUrl = await completion.CompleteMfaAsync(context, form["code"].FirstOrDefault() ?? string.Empty, ct);
    if (redirectUrl is null)
        return Results.Redirect("/Account/Mfa?error=invalid_code");

    return Results.Redirect(redirectUrl);
})
.AllowAnonymous()
.RequireRateLimiting("mfa");

// Logout confirmation page
app.MapGet("/Account/Logout", async (HttpContext httpContext) =>
{
    var returnUrl = httpContext.Request.Query["returnUrl"].FirstOrDefault() ?? "/";

    if (httpContext.User.Identity?.IsAuthenticated != true)
        return Results.Redirect("/Account/Login?returnUrl=" + System.Net.WebUtility.UrlEncode(returnUrl));

    var userName = httpContext.User.Identity.Name ?? "User";
    var html = BuildLogoutPage(userName, returnUrl);

    httpContext.Response.OnStarting(() =>
    {
        httpContext.Response.Headers.Remove("Content-Security-Policy");
        return Task.CompletedTask;
    });

    return Results.Content(html, "text/html; charset=utf-8");
})
.AllowAnonymous();

app.MapGet("/Account/Passkeys", (HttpContext context, IConfiguration configuration) =>
{
    if (context.User.Identity?.IsAuthenticated != true)
        return Results.Redirect("/Account/Login?returnUrl=%2FAccount%2FPasskeys");

    context.Response.OnStarting(() =>
    {
        context.Response.Headers.Remove("Content-Security-Policy");
        return Task.CompletedTask;
    });
    return Results.Content(
        BuildPasskeyManagementPage(
            configuration.GetValue("Ldap:Enabled", false),
            configuration.GetValue("Saml2:Enabled", false) && !string.IsNullOrWhiteSpace(configuration["Saml2:IdPMetadata"])),
        "text/html; charset=utf-8");
})
.AllowAnonymous();

static bool TryReadAuthorizeRequest(
    string returnUrl,
    out string clientId,
    out string redirectUri,
    out List<string> scopes,
    out string state)
{
    clientId = string.Empty;
    redirectUri = string.Empty;
    scopes = new List<string>();
    state = string.Empty;

    if (string.IsNullOrWhiteSpace(returnUrl) || returnUrl.Length > 8192 ||
        !returnUrl.StartsWith("/connect/authorize", StringComparison.Ordinal))
        return false;

    if (!Uri.TryCreate("http://localhost" + returnUrl, UriKind.Absolute, out var uri))
        return false;

    var query = QueryHelpers.ParseQuery(uri.Query);
    clientId = query["client_id"].FirstOrDefault() ?? string.Empty;
    redirectUri = query["redirect_uri"].FirstOrDefault() ?? string.Empty;
    state = query["state"].FirstOrDefault() ?? string.Empty;
    scopes = (query["scope"].FirstOrDefault() ?? string.Empty)
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    return !string.IsNullOrWhiteSpace(clientId) &&
           Uri.TryCreate(redirectUri, UriKind.Absolute, out _) &&
           scopes.Count > 0;
}

static HashSet<string> ParseConsentScopes(string scopesJson) =>
    new(JsonSerializer.Deserialize<List<string>>(scopesJson) ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

static bool HasCompletedMfa(ClaimsPrincipal principal) =>
    principal.Claims
        .SelectMany(claim => claim.Value.Trim('[', ']').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Any(value =>
            string.Equals(value.Trim('"'), "otp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value.Trim('"'), "passkey", StringComparison.OrdinalIgnoreCase));

static List<string> ParseStringList(string? json)
{
    if (string.IsNullOrWhiteSpace(json)) return new List<string>();
    try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
    catch (JsonException) { return new List<string>(); }
}

static string BuildAuthorizationErrorRedirect(string returnUrl, string error, string description)
{
    TryReadAuthorizeRequest(returnUrl, out _, out var redirectUri, out _, out var state);
    var query = new Dictionary<string, string?>
    {
        ["error"] = error,
        ["error_description"] = description,
        ["state"] = state
    };
    return QueryHelpers.AddQueryString(redirectUri, query);
}

/* static string BuildConsentPage(
    string displayName,
    string clientId,
    string redirectUri,
    IReadOnlyList<string> scopes,
    string protectedRequest,
    string state)
{
    var scopeDescriptions = new Dictionary<string, (string Title, string Description, string Icon)>(StringComparer.OrdinalIgnoreCase)
    {
        ["openid"] = ("Your identity", "Sign you in securely with His.Hope.", "person"),
        ["profile"] = ("Profile information", "Your name and basic profile details.", "badge"),
        ["email"] = ("Email address", "Your email address for account context.", "mail"),
        ["roles"] = ("Role information", "Your assigned roles for access control.", "admin_panel_settings"),
        ["offline_access"] = ("Stay signed in", "Allow the application to refresh your session when you return.", "refresh"),
        ["hishop:permissions"] = ("His.Hope permissions", "Permissions needed to show the right clinical workspace features.", "verified_user")
    };

    var permissionItems = string.Join("\n", scopes.Select(scope =>
    {
        var item = scopeDescriptions.GetValueOrDefault(scope, (scope, "Access requested by this application.", "key"));
        return $"<li><span class=\"scope-icon\"><span class=\"material-symbols\">{System.Net.WebUtility.HtmlEncode(item.Icon)}</span></span><span><strong>{System.Net.WebUtility.HtmlEncode(item.Title)}</strong><small>{System.Net.WebUtility.HtmlEncode(item.Description)}</small></span></li>";
    }));

    var safeClient = System.Net.WebUtility.HtmlEncode(displayName);
    var safeClientId = System.Net.WebUtility.HtmlEncode(clientId);
    var safeRedirect = System.Net.WebUtility.HtmlEncode(new Uri(redirectUri).Host);
    var safeRequest = System.Net.WebUtility.HtmlEncode(protectedRequest);
    var safeState = System.Net.WebUtility.HtmlEncode(state);

    var page = $"""
<!DOCTYPE html>
<html lang=""en""><head>
<meta charset=""utf-8"/><meta name=""viewport"" content=""width=device-width, initial-scale=1""/>
<title>Authorize {safeClient} — His.Hope</title>
<link rel=""preconnect"" href=""https://fonts.googleapis.com""/><link rel=""preconnect"" href=""https://fonts.gstatic.com"" crossorigin/>
<link href=""https://fonts.googleapis.com/css2?family=DM+Sans:wght@400;500;600;700&family=Manrope:wght@600;700;800&display=swap"" rel=""stylesheet""/>
<style>
*{{box-sizing:border-box}}:root{{--ink:#17352b;--muted:#6c7e75;--line:#dbe7df;--green:#176b4d;--soft:#edf7f0;--danger:#a53333}}
body{{margin:0;min-height:100vh;background:#f3f7f4;color:var(--ink);font-family:'DM Sans',sans-serif;display:grid;place-items:center;padding:28px 16px}}
.shell{{width:min(100%,760px);background:#fff;border:1px solid var(--line);border-radius:24px;box-shadow:0 24px 70px rgba(22,55,42,.14);overflow:hidden}}
.top{{padding:26px 32px 22px;background:#174b38;color:#fff;display:flex;align-items:center;justify-content:space-between;gap:20px}}
.brand{{display:flex;align-items:center;gap:12px;font-family:Manrope,sans-serif;font-weight:800;font-size:18px}}.mark{{width:36px;height:36px;border-radius:10px;background:#fff;color:#176b4d;display:grid;place-items:center;font-size:22px}}
.security{{font-size:12px;color:#c7e3d1;display:flex;gap:7px;align-items:center}}.dot{{width:8px;height:8px;border-radius:50%;background:#83d7a1}}
.content{{padding:34px 38px 36px}}.eyebrow{{font-size:11px;letter-spacing:.14em;text-transform:uppercase;color:#277553;font-weight:700;margin-bottom:10px}}
h1{{font-family:Manrope,sans-serif;font-size:30px;line-height:1.15;margin:0 0 9px;letter-spacing:-.02em}}.intro{{color:var(--muted);margin:0 0 25px;line-height:1.55}}
.client{{display:flex;align-items:center;gap:13px;padding:15px 16px;background:#f7faf8;border:1px solid var(--line);border-radius:14px;margin-bottom:22px}}.client-mark{{width:42px;height:42px;border-radius:12px;background:#d9eee1;color:#176b4d;display:grid;place-items:center;font-weight:800;font-size:18px}}.client strong{{display:block;font-size:15px}}.client small{{color:var(--muted);display:block;margin-top:3px;font-size:12px;word-break:break-all}}
.section-title{{font-weight:700;font-size:14px;margin:0 0 11px}}ul{{list-style:none;padding:0;margin:0 0 24px;border-top:1px solid var(--line)}}li{{display:flex;align-items:center;gap:13px;padding:14px 0;border-bottom:1px solid var(--line)}}.scope-icon{{width:34px;height:34px;border-radius:10px;background:var(--soft);color:var(--green);display:grid;place-items:center;flex:0 0 auto}}.material-symbols{{font-family:'Material Symbols Rounded';font-size:19px}}li strong,li small{{display:block}}li strong{{font-size:14px}}li small{{font-size:13px;color:var(--muted);margin-top:3px;line-height:1.4}}
.notice{{display:flex;gap:10px;padding:13px 14px;background:#fff9ec;border:1px solid #f0dfb3;border-radius:12px;color:#765b21;font-size:13px;line-height:1.45;margin-bottom:25px}}.notice b{{font-size:16px}}
.actions{{display:flex;gap:12px;justify-content:flex-end}}button{{min-height:46px;border-radius:11px;padding:0 20px;font:600 14px 'DM Sans',sans-serif;cursor:pointer}}.deny{{background:#fff;color:var(--ink);border:1px solid #b8c9be}}.allow{{background:var(--green);border:1px solid var(--green);color:#fff;box-shadow:0 8px 20px rgba(23,107,77,.2)}}button:focus-visible{{outline:3px solid #8bc9a2;outline-offset:2px}}
@media(max-width:600px){{body{{padding:0;display:block}}.shell{{min-height:100vh;border:0;border-radius:0;box-shadow:none}}.top{{padding:20px}}.content{{padding:28px 20px}}h1{{font-size:26px}}.actions{{flex-direction:column-reverse}}button{{width:100%}}}}
</style></head><body><main class=""shell"" aria-labelledby=""consent-title""><header class=""top""><div class=""brand""><span class=""mark"" aria-hidden=""true"">+</span>His.Hope</div><div class=""security""><span class=""dot"" aria-hidden=""true""></span>Secure authorization</div></header><section class=""content""><div class=""eyebrow"">Identity Service</div><h1 id=""consent-title"">Allow access to your account?</h1><p class=""intro"">Review what this application can access before continuing. You can revoke access later from your His.Hope account settings.</p><div class=""client""><div class=""client-mark"" aria-hidden=""true"">{System.Net.WebUtility.HtmlEncode(displayName[..Math.Min(displayName.Length, 1)].ToUpperInvariant())}</div><div><strong>{safeClient}</strong><small>{safeClientId} · Redirects to {safeRedirect}</small></div></div><h2 class=""section-title"">This application is requesting</h2><ul aria-label=""Requested permissions"">{permissionItems}</ul><div class=""notice"" role=""note""><b aria-hidden=""true"">!</b><span>Only approve applications you recognize. His.Hope records this decision for security and audit purposes.</span></div><form method=""post"" action=""/Account/Consent"" class=""actions""><input type=""hidden"" name=""request"" value=""{safeRequest}""/><input type=""hidden"" name=""state"" value=""{safeState}""/><button class=""deny"" type=""submit"" name=""decision"" value=""deny"">Deny</button><button class=""allow"" type=""submit"" name=""decision"" value=""allow"">Allow and continue</button></form></section></main></body></html>";
""";

    return page.Replace("\"\"", "\"");
} */

static string BuildConsentPage(string displayName, string clientId, string redirectUri, IReadOnlyCollection<string> scopes, string protectedRequest, string state)
{
    var descriptions = new Dictionary<string, (string Title, string Description)>(StringComparer.OrdinalIgnoreCase)
    {
        ["openid"] = ("Verify your identity", "Sign you in securely with OpenID Connect."),
        ["profile"] = ("Basic profile", "View your name and profile details."),
        ["email"] = ("Email address", "View the email address associated with your account."),
        ["roles"] = ("Assigned roles", "View roles needed to tailor access."),
        ["offline_access"] = ("Stay signed in", "Refresh your session without asking you to sign in again."),
        ["hishop:permissions"] = ("His.Hope permissions", "View permissions granted to you in His.Hope.")
    };
    var permissionItems = string.Join("", scopes.Select(scope =>
    {
        var item = descriptions.GetValueOrDefault(scope, (scope, "Access requested by this application."));
        return $"<li><span class=\"scope-icon\" aria-hidden=\"true\">+</span><span><strong>{System.Net.WebUtility.HtmlEncode(item.Item1)}</strong><small>{System.Net.WebUtility.HtmlEncode(item.Item2)}</small></span></li>";
    }));
    var template = """
<!DOCTYPE html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Authorize __CLIENT__ - His.Hope</title>
<style>
*{box-sizing:border-box}:root{--ink:#17352b;--muted:#6c7e75;--line:#dbe7df;--green:#176b4d;--soft:#edf7f0}body{margin:0;min-height:100vh;background:#f3f7f4;color:var(--ink);font-family:Arial,sans-serif;display:grid;place-items:center;padding:28px 16px}.shell{width:min(100%,760px);background:#fff;border:1px solid var(--line);border-radius:24px;box-shadow:0 24px 70px rgba(22,55,42,.14);overflow:hidden}.top{padding:26px 32px 22px;background:#174b38;color:#fff;display:flex;align-items:center;justify-content:space-between;gap:20px}.brand{font-weight:800;font-size:18px}.mark{display:inline-grid;place-items:center;width:36px;height:36px;margin-right:10px;border-radius:10px;background:#fff;color:#176b4d;font-size:22px}.security{font-size:12px;color:#c7e3d1}.content{padding:34px 38px 36px}.eyebrow{font-size:11px;letter-spacing:.14em;text-transform:uppercase;color:#277553;font-weight:700;margin-bottom:10px}h1{font-size:30px;line-height:1.15;margin:0 0 9px}.intro{color:var(--muted);margin:0 0 25px;line-height:1.55}.client{display:flex;align-items:center;gap:13px;padding:15px 16px;background:#f7faf8;border:1px solid var(--line);border-radius:14px;margin-bottom:22px}.client-mark{width:42px;height:42px;border-radius:12px;background:#d9eee1;color:#176b4d;display:grid;place-items:center;font-weight:800;font-size:18px}.client strong,.client small,li strong,li small{display:block}.client small,li small{color:var(--muted);margin-top:3px;font-size:13px}.section-title{font-weight:700;font-size:14px;margin:0 0 11px}ul{list-style:none;padding:0;margin:0 0 24px;border-top:1px solid var(--line)}li{display:flex;align-items:center;gap:13px;padding:14px 0;border-bottom:1px solid var(--line)}.scope-icon{width:34px;height:34px;border-radius:10px;background:var(--soft);color:var(--green);display:grid;place-items:center}.notice{padding:13px 14px;background:#fff9ec;border:1px solid #f0dfb3;border-radius:12px;color:#765b21;font-size:13px;line-height:1.45;margin-bottom:25px}.actions{display:flex;gap:12px;justify-content:flex-end}button{min-height:46px;border-radius:11px;padding:0 20px;font:600 14px Arial,sans-serif;cursor:pointer}.deny{background:#fff;color:var(--ink);border:1px solid #b8c9be}.allow{background:var(--green);border:1px solid var(--green);color:#fff}@media(max-width:600px){body{padding:0;display:block}.shell{min-height:100vh;border:0;border-radius:0;box-shadow:none}.top{padding:20px}.content{padding:28px 20px}.actions{flex-direction:column-reverse}button{width:100%}}
</style></head><body><main class="shell" aria-labelledby="consent-title"><header class="top"><div class="brand"><span class="mark" aria-hidden="true">+</span>His.Hope</div><div class="security">Secure authorization</div></header><section class="content"><div class="eyebrow">Identity Service</div><h1 id="consent-title">Allow access to your account?</h1><p class="intro">Review what this application can access before continuing. You can revoke access later from your His.Hope account settings.</p><div class="client"><div class="client-mark" aria-hidden="true">__INITIAL__</div><div><strong>__CLIENT__</strong><small>__CLIENT_ID__ - Redirects to __REDIRECT__</small></div></div><h2 class="section-title">This application is requesting</h2><ul aria-label="Requested permissions">__PERMISSIONS__</ul><div class="notice" role="note">Only approve applications you recognize. His.Hope records this decision for security and audit purposes.</div><form method="post" action="/Account/Consent" class="actions"><input type="hidden" name="request" value="__REQUEST__"><input type="hidden" name="state" value="__STATE__"><button class="deny" type="submit" name="decision" value="deny">Deny</button><button class="allow" type="submit" name="decision" value="allow">Allow and continue</button></form></section></main></body></html>
""";
    return template.Replace("__CLIENT__", System.Net.WebUtility.HtmlEncode(displayName), StringComparison.Ordinal)
        .Replace("__INITIAL__", System.Net.WebUtility.HtmlEncode(displayName[..Math.Min(displayName.Length, 1)].ToUpperInvariant()), StringComparison.Ordinal)
        .Replace("__CLIENT_ID__", System.Net.WebUtility.HtmlEncode(clientId), StringComparison.Ordinal)
        .Replace("__REDIRECT__", System.Net.WebUtility.HtmlEncode(new Uri(redirectUri).Host), StringComparison.Ordinal)
        .Replace("__PERMISSIONS__", permissionItems, StringComparison.Ordinal)
        .Replace("__REQUEST__", System.Net.WebUtility.HtmlEncode(protectedRequest), StringComparison.Ordinal)
        .Replace("__STATE__", System.Net.WebUtility.HtmlEncode(state), StringComparison.Ordinal);
}

static string BuildMfaPage(string? error, AdaptiveMfaMethods? methods)
{
    var availableMethods = methods?.AvailableMethods ?? [];
    var hasPasskey = availableMethods.Contains("passkey", StringComparer.Ordinal);
    var hasMobileApproval = availableMethods.Contains("mobileApproval", StringComparer.Ordinal);
    var hasTotp = availableMethods.Contains("totp", StringComparer.Ordinal);
    var preferredMethod = methods?.PreferredMethod ?? string.Empty;
    var mobilePrimary = string.Equals(preferredMethod, "mobileApproval", StringComparison.Ordinal) && hasMobileApproval;
    var alternateMethodsAvailable = (hasMobileApproval && !mobilePrimary) || hasTotp;
    var preferredMethodAttr = System.Net.WebUtility.HtmlEncode(preferredMethod);
    var methodsAttr = System.Net.WebUtility.HtmlEncode(string.Join(",", availableMethods));

    var alertMessage = methods is null
        ? "Your verification session has expired. Sign in again to continue securely."
        : string.Equals(error, "invalid_code", StringComparison.OrdinalIgnoreCase)
            ? "The verification code is invalid or has expired."
            : string.Empty;
    var errorBlock = string.IsNullOrWhiteSpace(alertMessage)
        ? string.Empty
        : $"""<p class="inline-alert" role="alert">{System.Net.WebUtility.HtmlEncode(alertMessage)}</p>""";

    var passkeyButtonHidden = hasPasskey ? string.Empty : " hidden";
    var nativeButtonHidden = hasMobileApproval ? string.Empty : " hidden";
    var alternateToggleHidden = alternateMethodsAvailable ? string.Empty : " hidden";
    var totpDisabled = hasTotp ? string.Empty : " disabled";
    var alternateToggleLabel = !hasPasskey && !hasMobileApproval && hasTotp
        ? "Use authenticator code"
        : "Use another method";

    var topLevelMobileButton = mobilePrimary
        ? $"""<button id="native-passkey-mfa" class="btn btn-secondary" type="button" aria-describedby="mfa-status"{nativeButtonHidden}>Approve in His.Hope mobile app</button>"""
        : string.Empty;
    var alternateMobileButton = mobilePrimary
        ? string.Empty
        : $"""<button id="native-passkey-mfa" class="btn btn-secondary" type="button" aria-describedby="mfa-status"{nativeButtonHidden}>Approve in His.Hope mobile app</button>""";

    var template = $$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Verify identity - His.Hope</title>
<style>
*{box-sizing:border-box}
body{margin:0;min-height:100vh;background:#eef3ef;color:#18251f;font:15px Arial,sans-serif;padding:32px 16px}
[hidden]{display:none!important}
.page{max-width:560px;margin:0 auto}
.brand{display:flex;align-items:center;gap:12px;margin-bottom:18px;color:#174b38;font-weight:800;letter-spacing:.02em}
.brand-mark{display:grid;place-items:center;width:40px;height:40px;border-radius:14px;background:#174b38;color:#fff;font-size:24px}
.card{background:#fff;border:1px solid #dbe6df;border-radius:24px;box-shadow:0 18px 45px #153b2a18;padding:30px}
.eyebrow{font-size:11px;font-weight:700;letter-spacing:.16em;text-transform:uppercase;color:#277553;margin-bottom:10px}
h1{margin:0 0 10px;font-size:30px;line-height:1.12}
.intro{margin:0 0 24px;color:#5f7067;line-height:1.6}
.inline-alert{margin:0 0 16px;padding:12px 14px;border-radius:12px;background:#fff1ed;border:1px solid #ffd5c9;color:#9f2f1f;line-height:1.5}
.feedback{min-height:22px;margin:0 0 12px;line-height:1.5}
#mfa-status{color:#325744}
#mfa-error{color:#9f2f1f}
.action-stack{display:grid;gap:12px}
.btn{display:inline-flex;align-items:center;justify-content:center;min-height:50px;width:100%;padding:0 16px;border-radius:14px;border:1px solid #216344;background:#216344;color:#fff;font:700 15px Arial,sans-serif;cursor:pointer;transition:transform .18s,box-shadow .18s,background .18s}
.btn:hover{background:#1b5439;transform:translateY(-1px);box-shadow:0 12px 22px rgba(33,99,68,.18)}
.btn:disabled{cursor:not-allowed;opacity:.65;transform:none;box-shadow:none}
.btn-secondary{background:#fff;color:#1d392e;border-color:#b9c9bf}
.btn-secondary:hover{background:#f6faf7}
.btn-link{background:transparent;color:#216344;border-color:#dbe6df;box-shadow:none}
.btn-link:hover{background:#f6faf7;box-shadow:none}
.alternate-panel{margin-top:14px;padding-top:16px;border-top:1px solid #e4ece7}
.alternate-copy{margin:0 0 14px;color:#66756c;line-height:1.5}
.totp-form{display:grid;gap:10px;margin-top:14px}
.totp-form label{font-weight:700;color:#325744}
.totp-form input{min-height:48px;padding:0 14px;border:1px solid #b9c9bf;border-radius:12px;font:inherit;letter-spacing:.12em}
.totp-form input:focus,.btn:focus-visible{outline:3px solid rgba(47,125,85,.18);outline-offset:2px}
.support{margin-top:18px;font-size:13px;color:#607067;line-height:1.5}
@media (max-width:640px){
  body{padding:0}
  .page{max-width:none}
  .card{min-height:100vh;border-radius:0;border:0;box-shadow:none;padding:26px 20px 32px}
}
</style>
<noscript>
  <style>
    #alternate-method-panel[hidden],#totp-form[hidden]{display:block!important}
  </style>
</noscript>
</head>
<body>
  <main class="page">
    <div class="brand"><span class="brand-mark" aria-hidden="true">+</span>His.Hope</div>
      <section class="card"
      data-mfa-methods-endpoint="/api/v1/auth/mfa/methods"
      data-native-hardware-unverified="true"
      data-preferred-method="{{preferredMethodAttr}}"
      data-available-methods="{{methodsAttr}}">
      <div class="eyebrow">Identity Service</div>
      <h1>Verify your identity</h1>
      <p class="intro">Use a trusted factor to finish this hospital sign-in without restarting the OIDC session.</p>
      {{errorBlock}}
      <p id="mfa-status" class="feedback" role="status" aria-live="polite"></p>
      <p id="mfa-error" class="feedback" role="alert" aria-live="assertive" hidden></p>
      <div id="primary-actions" class="action-stack">
        {{topLevelMobileButton}}
        <button id="passkey-mfa" class="btn" type="button" aria-describedby="mfa-status"{{passkeyButtonHidden}}>Continue with device passkey</button>
        <button id="alternate-methods" class="btn btn-link" type="button" aria-controls="alternate-method-panel" aria-expanded="false"{{alternateToggleHidden}}>{{alternateToggleLabel}}</button>
      </div>
      <section id="alternate-method-panel" class="alternate-panel" hidden aria-label="Alternate verification methods">
        <p class="alternate-copy">Choose another trusted method if the device passkey is unavailable right now.</p>
        <div id="alternate-actions" class="action-stack">
          {{alternateMobileButton}}
        </div>
        <form id="totp-form" class="totp-form" method="post" action="/Account/Mfa" hidden>
          <label for="totp-code">Authenticator code</label>
          <input id="totp-code" name="code" inputmode="numeric" autocomplete="one-time-code" pattern="[0-9]{6}" minlength="6" maxlength="6"{{totpDisabled}}>
          <button type="submit" class="btn"{{totpDisabled}}>Verify with TOTP</button>
        </form>
      </section>
      <p class="support">Verification methods stay bound to the pending browser session. The page never asks for your account email again.</p>
    </section>
  </main>
  <script src="/api/v1/auth/identity-login.js?v=20260801-passkey" defer></script>
</body>
</html>
""";

    return template;
}

static string ResolveIdentityLoginScriptPath(IWebHostEnvironment environment)
{
    var candidates = new[]
    {
        string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? null
            : Path.Combine(environment.WebRootPath, "js", "identity-login.js"),
        Path.Combine(environment.ContentRootPath, "wwwroot", "js", "identity-login.js"),
        Path.Combine(AppContext.BaseDirectory, "wwwroot", "js", "identity-login.js")
    }
    .Where(path => !string.IsNullOrWhiteSpace(path))
    .Cast<string>()
    .ToList();

    foreach (var candidate in candidates)
    {
        if (File.Exists(candidate))
            return candidate;
    }

    foreach (var root in EnumerateSearchRoots(environment.ContentRootPath)
        .Concat(EnumerateSearchRoots(Directory.GetCurrentDirectory()))
        .Distinct(StringComparer.OrdinalIgnoreCase))
    {
        var sourceCandidate = Path.Combine(root, "src", "Services", "IdentityService", "IdentityService.Api", "wwwroot", "js", "identity-login.js");
        if (File.Exists(sourceCandidate))
            return sourceCandidate;
    }

    return candidates[0];
}

static IEnumerable<string> EnumerateSearchRoots(string? startPath)
{
    if (string.IsNullOrWhiteSpace(startPath))
        yield break;

    for (var current = new DirectoryInfo(startPath); current is not null; current = current.Parent)
    {
        yield return current.FullName;
    }
}

static string BuildPasskeyManagementPage(bool ldapAvailable, bool samlAvailable)
{
    var ldapCard = ldapAvailable
        ? "<a class='provider-action' href='/Account/Login?returnUrl=%2FAccount%2FPasskeys'>Sign in with LDAP/AD</a><span class='provider-status ready'>Configured</span>"
        : "<span class='provider-action disabled'>LDAP/AD sign-in unavailable</span><span class='provider-status'>Configure Ldap settings</span>";
    var samlCard = samlAvailable
        ? "<a class='provider-action' href='/api/v1/federation/saml/login?returnUrl=%2FAccount%2FPasskeys'>Continue with SAML SSO</a><span class='provider-status ready'>Configured</span>"
        : "<span class='provider-action disabled'>SAML SSO unavailable</span><span class='provider-status'>Configure IdP metadata</span>";

    return @"<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Security methods - His.Hope</title><style>body{margin:0;background:#eef3ef;color:#18251f;font:15px Arial,sans-serif}.page{max-width:920px;margin:40px auto;padding:32px;background:#fff;border:1px solid #dbe6df;border-radius:22px;box-shadow:0 18px 45px #153b2a18}h1{margin:0 0 8px;font-size:30px}p{color:#66756c}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:16px;margin-top:24px}.card{display:grid;gap:10px;padding:20px;border:1px solid #dbe6df;border-radius:16px;background:#f8fbf8}.card h2{margin:0;font-size:18px}.provider-action{display:inline-flex;align-items:center;justify-content:center;min-height:42px;padding:0 14px;border-radius:10px;background:#216344;color:#fff;text-decoration:none;font-weight:700}.provider-action.disabled{background:#e6ece8;color:#738078;cursor:not-allowed}.provider-status{font-size:12px;color:#738078}.provider-status.ready{color:#216344;font-weight:700}#register{min-height:46px;padding:0 18px;border:0;border-radius:10px;background:#216344;color:#fff;font-weight:700;cursor:pointer}#status{min-height:20px}</style></head><body><main class='page'><h1>Security methods</h1><p>Register a passkey for this account or use an enabled hospital identity provider.</p><div class='grid'><section class='card'><h2>Passkey</h2><p>Use Face ID, fingerprint or your device security key for OIDC sign-in.</p><button id='register' type='button'>Create passkey</button><p id='status' role='status'></p></section><section class='card'><h2>LDAP/AD</h2><p>Hospital directory authentication.</p>" + ldapCard + @"</section><section class='card'><h2>SAML SSO</h2><p>Enterprise identity provider authentication.</p>" + samlCard + @"</section></div></main><script src='/api/v1/auth/identity-login.js?v=20260801-passkey' defer></script></body></html>";
}

static string BuildLoginPage(bool hasError, string errorMessage, string encodedReturnUrl, List<string> externalProviders, bool samlAvailable)
{
    var errorBlock = hasError
        ? $"<div class=\"mat-error\" role=\"alert\"><svg viewBox=\"0 0 24 24\" fill=\"currentColor\"><path d=\"M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z\"/></svg>{System.Net.WebUtility.HtmlEncode(errorMessage)}</div>"
        : "";

    var extBlock = "";
    if (externalProviders.Count > 0)
    {
        var btns = string.Join("\n", externalProviders.Select(p =>
            $"<form method=\"post\" action=\"/Account/ExternalLogin\"><input type=\"hidden\" name=\"provider\" value=\"{System.Net.WebUtility.HtmlEncode(p)}\" /><input type=\"hidden\" name=\"returnUrl\" value=\"{encodedReturnUrl}\" /><button type=\"submit\" class=\"btn-secondary\"><svg viewBox=\"0 0 24 24\" fill=\"currentColor\"><path d=\"M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z\"/></svg>Continue with {System.Net.WebUtility.HtmlEncode(p)}</button></form>"));
        extBlock = $"<div class=\"external-section\">{btns}</div>";
    }

    var samlBlock = samlAvailable
        ? $"<a class=\"btn-secondary link-button\" href=\"/api/v1/federation/saml/login?returnUrl={System.Net.WebUtility.UrlEncode(System.Net.WebUtility.HtmlDecode(encodedReturnUrl))}\">Continue with enterprise SSO (SAML)</a>"
        : "<button type=\"button\" class=\"btn-secondary provider-disabled\" disabled title=\"Configure SAML IdP metadata to enable enterprise SSO\">Enterprise SSO (SAML) unavailable</button>";
    var federationBlock = $@"
      <div class=""federation-section"" aria-label=""Alternative sign in methods"">
        <button type=""button"" class=""btn-secondary"" id=""passkey-button"">Sign in with a passkey</button>
        <form method=""post"" action=""/Account/LdapLogin"" class=""ldap-form""><input type=""hidden"" name=""returnUrl"" value=""{encodedReturnUrl}""/><label for=""ldap-user"">Hospital directory account</label><input id=""ldap-user"" name=""userName"" type=""text"" autocomplete=""username"" placeholder=""AD username or email"" required/><label for=""ldap-password"">Directory password</label><input id=""ldap-password"" name=""password"" type=""password"" autocomplete=""current-password"" placeholder=""Directory password"" required/><button type=""submit"" class=""btn-secondary"">Sign in with LDAP/AD</button></form>
        {samlBlock}
        <p id=""passkey-error"" class=""federation-error"" role=""alert"" hidden></p>
      </div>
      <script src=""/api/v1/auth/identity-login.js"" defer></script>";

    return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""utf-8""/>
<meta name=""viewport"" content=""width=device-width, initial-scale=1""/>
<title>Sign in — His.Hope HIS</title>
<style>
*,*::before,*::after{{box-sizing:border-box;margin:0;padding:0}}
html,body{{min-height:100%}}
body{{
  min-height:100%;
  font-family:'Aptos','Segoe UI',Roboto,-apple-system,BlinkMacSystemFont,sans-serif;
  font-size:15px;font-weight:400;line-height:1.5;
  color:#18251f;background:#eef3ef;
  -webkit-font-smoothing:antialiased;text-rendering:geometricPrecision
}}
.login-page{{
  min-height:100dvh;display:grid;place-items:center;padding:32px 18px;
  background:
    radial-gradient(circle at 16% 18%,rgba(46,125,84,.16),transparent 30%),
    radial-gradient(circle at 78% 12%,rgba(37,70,58,.13),transparent 28%),
    linear-gradient(135deg,#f7faf6 0%,#edf3ee 44%,#dfe9e2 100%);
  position:relative;overflow:hidden
}}
.login-page::before{{
  content:"""";position:absolute;inset:0;pointer-events:none;opacity:.32;
  background-image:
    linear-gradient(rgba(24,37,31,.05) 1px,transparent 1px),
    linear-gradient(90deg,rgba(24,37,31,.05) 1px,transparent 1px);
  background-size:44px 44px;
  mask-image:linear-gradient(to bottom,rgba(0,0,0,.75),transparent 82%)
}}
.shell{{
  width:min(100%,940px);position:relative;z-index:1;
  display:grid;grid-template-columns:minmax(300px,.9fr) minmax(340px,1fr);
  background:rgba(255,255,255,.74);border:1px solid rgba(50,74,63,.16);
  box-shadow:0 24px 70px rgba(21,45,35,.18),0 8px 24px rgba(21,45,35,.09);
  border-radius:28px;overflow:hidden;animation:card-in .42s cubic-bezier(.2,.8,.2,1);
  backdrop-filter:blur(18px)
}}
@keyframes card-in{{from{{opacity:0;transform:translateY(18px) scale(.985)}}to{{opacity:1;transform:translateY(0) scale(1)}}}}
.brand-panel{{
  min-height:560px;padding:36px;display:flex;flex-direction:column;justify-content:space-between;
  color:#f7fbf8;background:
    radial-gradient(circle at 24% 18%,rgba(255,255,255,.18),transparent 28%),
    linear-gradient(145deg,#153b2a 0%,#23533d 58%,#2f6e50 100%)
}}
.brand-mark{{display:flex;align-items:center;gap:12px;font-weight:700;font-size:20px;letter-spacing:.1px}}
.brand-mark span{{
  width:42px;height:42px;border-radius:12px;display:grid;place-items:center;
  background:#f7fbf8;color:#236344;box-shadow:0 12px 30px rgba(0,0,0,.18)
}}
.brand-mark svg{{width:24px;height:24px}}
.brand-copy h1{{font-size:46px;line-height:.96;font-weight:750;letter-spacing:0;margin-bottom:18px;text-wrap:balance}}
.brand-copy p{{max-width:29rem;color:rgba(247,251,248,.78);font-size:16px;line-height:1.7}}
.assurance{{display:grid;gap:10px;color:rgba(247,251,248,.78);font-size:13px}}
.assurance div{{display:flex;align-items:center;gap:10px}}
.assurance svg{{width:17px;height:17px;color:#bfe7cf}}
.card-body{{padding:48px 44px;background:rgba(255,255,255,.94);display:flex;flex-direction:column;justify-content:center}}
.eyebrow{{font-size:12px;font-weight:700;color:#2c684a;letter-spacing:.14em;text-transform:uppercase;margin-bottom:10px}}
.card-body h2{{font-size:32px;line-height:1.1;font-weight:760;letter-spacing:0;color:#14221b;margin-bottom:10px}}
.intro{{color:#65736b;margin-bottom:30px;max-width:34rem}}
.mat-error{{
  display:flex;align-items:flex-start;gap:10px;padding:12px 14px;margin-bottom:18px;
  background:#fff1ed;color:#9f2f1f;border:1px solid #ffd5c9;border-radius:12px;font-size:14px;
  animation:shake .35s ease
}}
@keyframes shake{{
0%,100%{{transform:translateX(0)}}20%{{transform:translateX(-8px)}}40%{{transform:translateX(8px)}}60%{{transform:translateX(-4px)}}80%{{transform:translateX(4px)}}
}}
.mat-error svg{{width:20px;height:20px;flex-shrink:0}}
.field{{display:block;margin-bottom:18px;position:relative}}
.field label{{
  display:block;font-size:13px;font-weight:650;color:#34483d;
  margin-bottom:8px;letter-spacing:0
}}
.field-inner{{position:relative}}
.field-inner input{{
  width:100%;height:52px;padding:0 14px 0 46px;
  border:1px solid #cfdcd4;border-radius:14px;
  font-family:inherit;font-size:15px;font-weight:500;color:#16241d;
  background:#f8fbf8;outline:none;
  transition:border-color .18s,background .18s,box-shadow .18s,transform .18s
}}
.field-inner input::placeholder{{color:#8a978f}}
.field-inner input:hover{{background:#fff;border-color:#9eb5a8}}
.field-inner input:focus{{
  background:#fff;border-color:#2f7d55;
  box-shadow:0 0 0 4px rgba(47,125,85,.14);transform:translateY(-1px)
}}
.field-icon{{
  position:absolute;left:15px;top:50%;transform:translateY(-50%);
  pointer-events:none;transition:color .2s
}}
.field-icon svg{{width:19px;height:19px;color:#7b8981;display:block}}
.field-inner input:focus~.field-icon svg{{color:#2f7d55}}
.btn{{
  display:inline-flex;align-items:center;justify-content:center;gap:8px;
  width:100%;min-height:52px;padding:0 18px;border:none;border-radius:14px;
  font-family:inherit;font-size:15px;font-weight:750;
  letter-spacing:0;cursor:pointer;
  background:#216344;color:#fff;
  box-shadow:0 14px 28px rgba(33,99,68,.22),inset 0 1px 0 rgba(255,255,255,.18);
  transition:background .18s,box-shadow .18s,transform .18s;user-select:none
}}
.btn:hover{{background:#1b5439;box-shadow:0 18px 34px rgba(33,99,68,.28);transform:translateY(-1px)}}
.btn:active{{background:#17472f;box-shadow:0 8px 18px rgba(33,99,68,.2);transform:translateY(1px) scale(.995)}}
.btn:focus-visible,.btn-secondary:focus-visible,input:focus-visible{{outline:3px solid rgba(47,125,85,.24);outline-offset:3px}}
.btn svg{{width:18px;height:18px}}
.external-section{{margin-top:18px;display:flex;flex-direction:column;gap:10px}}
.btn-secondary{{
  display:inline-flex;align-items:center;justify-content:center;gap:8px;
  width:100%;min-height:48px;padding:0 16px;
  border:1px solid #cfdcd4;border-radius:14px;
  font-family:inherit;font-size:14px;font-weight:700;
  color:#21362b;background:#fff;cursor:pointer;
  letter-spacing:0;transition:background .18s,border-color .18s,transform .18s
}}
.btn-secondary:hover{{background:#f5faf6;border-color:#9eb5a8;transform:translateY(-1px)}}
.btn-secondary svg{{width:20px;height:20px;color:#2f7d55}}
.federation-section{{margin-top:18px;display:grid;gap:10px}}
.ldap-form{{display:grid;gap:8px;padding:14px;border:1px solid #dbe6df;border-radius:14px;background:#f8fbf8}}
.ldap-form label{{font-size:12px;font-weight:700;color:#496055}}
.ldap-form input{{width:100%;height:42px;padding:0 12px;border:1px solid #cfdcd4;border-radius:10px;background:#fff;color:#18251f;font:inherit;outline:none}}
.ldap-form input:focus{{border-color:#2f7d55;box-shadow:0 0 0 3px rgba(47,125,85,.12)}}
.ldap-form .btn-secondary{{margin-top:4px}}
.link-button{{text-decoration:none}}
.provider-disabled{{cursor:not-allowed;opacity:.62}}
.federation-error{{margin:0;padding:10px 12px;border-radius:10px;background:#fff1ed;color:#9f2f1f;border:1px solid #ffd5c9;font-size:13px}}
.footer{{
  margin-top:18px;text-align:center;font-size:12px;font-weight:500;
  color:#607067
}}
.footer strong{{color:#21362b;font-weight:800}}
@media (max-width:780px){{
  .login-page{{padding:18px}}
  .shell{{grid-template-columns:1fr;border-radius:22px}}
  .brand-panel{{min-height:auto;padding:26px}}
  .brand-copy{{margin:42px 0 22px}}
  .brand-copy h1{{font-size:34px}}
  .assurance{{display:none}}
  .card-body{{padding:30px 24px}}
  .card-body h2{{font-size:27px}}
}}
</style>
</head>
<body>
<div class=""login-page"">
  <main class=""shell"" aria-label=""His.Hope sign in"">
    <section class=""brand-panel"" aria-label=""His.Hope identity"">
      <div class=""brand-mark"">
        <span><svg viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2ZM11 17H8v-3H5v-3h3V8h3v3h3v3h-3v3Zm7-1.5h-3v-2h3v2Zm0-4h-3v-2h3v2Z""/></svg></span>
        His.Hope
      </div>
      <div class=""brand-copy"">
        <h1>Clinical access, protected.</h1>
        <p>Sign in to continue to the His.Hope hospital information workspace with audited, role-aware access.</p>
      </div>
      <div class=""assurance"">
        <div><svg viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M12 1 3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4Zm-1 15-4-4 1.41-1.41L11 13.17l5.59-5.59L18 9l-7 7Z""/></svg>HIPAA-oriented security controls</div>
        <div><svg viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M12 3a9 9 0 0 0-9 9h3a6 6 0 1 1 1.76 4.24L6 18h6v-6l-2.12 2.12A3 3 0 1 0 9 12H6a6 6 0 1 1 6 6c-1.66 0-3.14-.67-4.22-1.76L5.66 18.36A9 9 0 1 0 12 3Z""/></svg>Session sync across His.Hope apps</div>
      </div>
    </section>
    <section class=""card-body"">
      <div class=""eyebrow"">Identity Service</div>
      <h2>Welcome back</h2>
      <p class=""intro"">Use your hospital account to continue.</p>
      {errorBlock}
      <form method=""post"" action=""/Account/Login"" autocomplete=""off"">
        <input type=""hidden"" name=""returnUrl"" value=""{encodedReturnUrl}""/>
        <div class=""field"">
          <label for=""email"">Email address</label>
          <div class=""field-inner"">
            <input type=""email"" id=""email"" name=""email"" placeholder=""admin@hishop.com"" required autocomplete=""username""/>
            <span class=""field-icon""><svg viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M20 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4-8 5-8-5V6l8 5 8-5v2z""/></svg></span>
          </div>
        </div>
        <div class=""field"">
          <label for=""password"">Password</label>
          <div class=""field-inner"">
            <input type=""password"" id=""password"" name=""password"" placeholder=""Enter your password"" required autocomplete=""current-password""/>
            <span class=""field-icon""><svg viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M18 8h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zm-6 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zm3.1-9H8.9V6c0-1.71 1.39-3.1 3.1-3.1s3.1 1.39 3.1 3.1v2z""/></svg></span>
          </div>
        </div>
        <button type=""submit"" class=""btn"">
          Sign in
          <svg viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M12 4 10.59 5.41 16.17 11H4v2h12.17l-5.58 5.59L12 20l8-8-8-8Z""/></svg>
        </button>
      </form>
      {extBlock}
      {federationBlock}
    </section>
  </main>
  <div class=""footer"">
    <strong>His.Hope</strong> v1.0 &bull; Identity and access management
  </div>
</div>
</body>
</html>";
}

static string BuildAlreadySignedInPage(string userName, string returnUrl)
{
    var encodedReturnUrl = System.Net.WebUtility.HtmlEncode(returnUrl);
    var encodedUserName = System.Net.WebUtility.HtmlEncode(userName);
    return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""utf-8""/>
<meta name=""viewport"" content=""width=device-width, initial-scale=1""/>
<title>Already signed in — His.Hope HIS</title>
<style>
*,*::before,*::after{{box-sizing:border-box;margin:0;padding:0}}
html,body{{min-height:100%}}
body{{
  min-height:100%;font-family:'Aptos','Segoe UI',Roboto,-apple-system,BlinkMacSystemFont,sans-serif;
  font-size:15px;line-height:1.5;color:#18251f;background:#eef3ef;
  -webkit-font-smoothing:antialiased
}}
.login-page{{
  min-height:100dvh;display:grid;place-items:center;padding:32px 18px;
  background:radial-gradient(circle at 16% 18%,rgba(46,125,84,.16),transparent 30%),radial-gradient(circle at 78% 12%,rgba(37,70,58,.13),transparent 28%),linear-gradient(135deg,#f7faf6 0%,#edf3ee 44%,#dfe9e2 100%);position:relative;overflow:hidden
}}
.login-page::before{{content:"""";position:absolute;inset:0;pointer-events:none;opacity:.32;background-image:linear-gradient(rgba(24,37,31,.05) 1px,transparent 1px),linear-gradient(90deg,rgba(24,37,31,.05) 1px,transparent 1px);background-size:44px 44px;mask-image:linear-gradient(to bottom,rgba(0,0,0,.75),transparent 82%)}}
.card{{
  width:min(100%,940px);display:grid;grid-template-columns:minmax(300px,.9fr) minmax(340px,1fr);position:relative;z-index:1;background:rgba(255,255,255,.78);border:1px solid rgba(50,74,63,.16);border-radius:28px;overflow:hidden;box-shadow:0 24px 70px rgba(21,45,35,.18),0 8px 24px rgba(21,45,35,.09);animation:card-in .42s cubic-bezier(.2,.8,.2,1);backdrop-filter:blur(18px)
}}
@keyframes card-in{{from{{opacity:0;transform:translateY(18px) scale(.985)}}to{{opacity:1;transform:translateY(0) scale(1)}}}}
.brand-panel{{min-height:520px;padding:36px;display:flex;flex-direction:column;justify-content:space-between;color:#f7fbf8;background:radial-gradient(circle at 24% 18%,rgba(255,255,255,.18),transparent 28%),linear-gradient(145deg,#153b2a 0%,#23533d 58%,#2f6e50 100%)}}
.brand-mark{{display:flex;align-items:center;gap:12px;font-weight:700;font-size:20px}}
.brand-mark span{{width:42px;height:42px;border-radius:12px;display:grid;place-items:center;background:#f7fbf8;color:#236344;box-shadow:0 12px 30px rgba(0,0,0,.18)}}
.brand-mark svg{{width:24px;height:24px}}
.brand-copy h1{{font-size:44px;line-height:.98;font-weight:750;margin-bottom:18px}}
.brand-copy p{{max-width:29rem;color:rgba(247,251,248,.78);font-size:16px;line-height:1.7}}
.assurance{{display:grid;gap:10px;color:rgba(247,251,248,.78);font-size:13px}}
.assurance div{{display:flex;align-items:center;gap:10px}}
.assurance svg{{width:17px;height:17px;color:#bfe7cf}}
.card-body{{padding:48px 44px;background:rgba(255,255,255,.94);display:flex;flex-direction:column;justify-content:center}}
svg{{display:block}}
.eyebrow{{font-size:12px;font-weight:700;color:#2c684a;letter-spacing:.14em;text-transform:uppercase;margin-bottom:10px}}
.card-body h2{{font-size:32px;line-height:1.1;font-weight:760;color:#14221b;margin-bottom:10px}}
.intro{{color:#65736b;margin-bottom:30px;line-height:1.6}}
.identity{{display:flex;align-items:center;gap:12px;margin-bottom:24px;padding:14px 16px;border:1px solid #dbe7df;border-radius:14px;background:#f7faf8;color:#34483d}}
.identity svg{{width:22px;height:22px;color:#2f7d55;flex:0 0 auto}}
.identity strong{{display:block;color:#14221b;font-size:16px}}
.identity small{{display:block;margin-top:2px;color:#748279;font-size:12px}}
.btn{{
  display:inline-flex;align-items:center;justify-content:center;gap:8px;
  width:100%;min-height:50px;padding:0 18px;border:1px solid #216344;border-radius:14px;
  font-family:inherit;font-size:15px;font-weight:700;cursor:pointer;text-decoration:none;background:#216344;color:#fff;
  box-shadow:0 12px 24px rgba(33,99,68,.16);transition:background .18s,transform .18s,box-shadow .18s;user-select:none
}}
.btn:hover{{background:#1b5439;transform:translateY(-1px);box-shadow:0 16px 28px rgba(33,99,68,.22)}}
.btn:active{{background:#174b35;transform:translateY(0)}}
.btn svg{{width:18px;height:18px}}
.secondary-actions{{display:grid;gap:12px;margin-top:16px}}
.text-link{{display:inline-flex;justify-content:center;padding:9px;color:#216344;text-decoration:none;font-size:14px;font-weight:650;border-radius:10px}}
.text-link:hover{{background:#f1f7f3;text-decoration:underline}}
.footer{{position:relative;z-index:1;text-align:center;padding:18px 0 0;font-size:12px;color:#718078}}
.footer strong{{color:#23533d;font-weight:700}}
@media(max-width:720px){{.card{{grid-template-columns:1fr;max-width:560px}}.brand-panel{{min-height:250px;padding:28px}}.brand-copy h1{{font-size:36px}}.assurance{{display:none}}.card-body{{padding:34px 26px}}}}
</style>
</head>
<body>
<div class=""login-page"">
  <div class=""card"">
    <div class=""brand-panel"">
      <div class=""brand-mark""><span><svg viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-7 3c1.93 0 3.5 1.57 3.5 3.5S13.93 13 12 13s-3.5-1.57-3.5-3.5S10.07 6 12 6zm7 13H5v-.23c0-.62.28-1.2.76-1.58C7.47 15.82 9.64 15 12 15s4.53 2.19 6.24 2.19c.48.38.76.97.76 1.58V19z""/></svg></span>His.Hope</div>
      <div class=""brand-copy""><div class=""eyebrow"" style=""color:#bfe7cf"">Identity Service</div><h1>Clinical access,<br>protected.</h1><p>Continue to the His.Hope hospital information workspace with audited, role-aware access.</p></div>
      <div class=""assurance""><div><svg viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M12 2 4 5v6c0 5.2 3.4 10 8 11 4.6-1 8-5.8 8-11V5l-8-3zm-1 14-3-3 1.4-1.4L11 13.2l3.6-3.6L16 11l-5 5z""/></svg>HIPAA-oriented security controls</div><div><svg viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M12 3a9 9 0 1 0 8.5 12H18a6 6 0 1 1-1.8-6.2L14 11h7V4l-2.1 2.1A8.9 8.9 0 0 0 12 3z""/></svg>Session sync across His.Hope apps</div></div>
    </div>
    <div class=""card-body"">
      <div class=""eyebrow"">Identity Service</div><h2>Welcome back</h2><p class=""intro"">Your session is active and ready to continue.</p>
      <div class=""identity""><svg viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M12 2a5 5 0 1 0 0 10 5 5 0 0 0 0-10zm0 2a3 3 0 1 1 0 6 3 3 0 0 1 0-6zm0 10c-4.4 0-8 2.2-8 5v3h16v-3c0-2.8-3.6-5-8-5zm-6 6c.2-1.1 2.6-3 6-3s5.8 1.9 6 3H6z""/></svg><div><small>Signed in as</small><strong>{encodedUserName}</strong></div></div>
      <a href=""{encodedReturnUrl}"" class=""btn""><svg viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z""/></svg>Continue to workspace</a>
      <div class=""secondary-actions""><a href=""/Account/Passkeys"" class=""text-link"">Manage passkeys</a><a href=""/Account/Logout?returnUrl={encodedReturnUrl}"" class=""text-link"">Sign out</a></div>
    </div>
  </div>
  <div class=""footer"">
    <strong>His.Hope</strong> v1.0 &bull; Identity and access management
  </div>
</div>
</body>
</html>";
}

static string BuildLogoutPage(string userName, string returnUrl)
{
    var encodedReturnUrl = System.Net.WebUtility.HtmlEncode(returnUrl);
    var encodedUserName = System.Net.WebUtility.HtmlEncode(userName);
    return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""utf-8""/>
<meta name=""viewport"" content=""width=device-width, initial-scale=1""/>
<title>Sign out — His.Hope HIS</title>
<style>
*,*::before,*::after{{box-sizing:border-box;margin:0;padding:0}}
html,body{{height:100%}}
body{{
  font-family:Roboto,-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;
  font-size:14px;font-weight:400;line-height:1.5;
  color:rgba(0,0,0,.87);background:#fafafa;
  -webkit-font-smoothing:antialiased
}}
.login-page{{
  min-height:100vh;display:flex;flex-direction:column;align-items:center;
  justify-content:center;padding:16px;
  background:linear-gradient(135deg,#3f51b5,#303f9f)
}}
.card{{
  width:100%;max-width:400px;background:#fff;border-radius:4px;
  box-shadow:0 5px 5px -3px rgba(0,0,0,.2),0 8px 10px 1px rgba(0,0,0,.14),0 3px 14px 2px rgba(0,0,0,.12);
  overflow:hidden;animation:card-in .3s cubic-bezier(.4,0,.2,1)
}}
@keyframes card-in{{from{{opacity:0;transform:translateY(24px) scale(.98)}}to{{opacity:1;transform:translateY(0) scale(1)}}}}
.card-header{{
  background:linear-gradient(135deg,#3f51b5,#303f9f);color:#fff;
  padding:40px 24px 32px;text-align:center
}}
.card-header svg{{width:48px;height:48px;margin-bottom:12px;opacity:.9}}
.card-header h1{{font-size:24px;font-weight:400;margin:0 0 4px;letter-spacing:.25px}}
.card-header p{{font-size:14px;font-weight:300;opacity:.8;letter-spacing:.1px}}
.card-body{{padding:24px}}
.btn{{
  display:inline-flex;align-items:center;justify-content:center;gap:8px;
  width:100%;min-height:36px;padding:0 16px;border:none;border-radius:4px;
  font-family:inherit;font-size:14px;font-weight:500;
  letter-spacing:.75px;text-transform:uppercase;cursor:pointer;
  background:#3f51b5;color:#fff;
  box-shadow:0 3px 1px -2px rgba(0,0,0,.2),0 2px 2px 0 rgba(0,0,0,.14),0 1px 5px 0 rgba(0,0,0,.12);
  transition:background .2s,box-shadow .2s;user-select:none
}}
.btn:hover{{background:#3949ab;box-shadow:0 2px 4px -1px rgba(0,0,0,.2),0 4px 5px 0 rgba(0,0,0,.14),0 1px 10px 0 rgba(0,0,0,.12)}}
.btn:active{{background:#303f9f;box-shadow:0 5px 5px -3px rgba(0,0,0,.2),0 8px 10px 1px rgba(0,0,0,.14),0 3px 14px 2px rgba(0,0,0,.12)}}
.btn svg{{width:18px;height:18px}}
.footer{{
  text-align:center;padding:24px 0 0;font-size:12px;font-weight:400;
  color:rgba(255,255,255,.7)
}}
.footer strong{{color:#fff;font-weight:500}}
</style>
</head>
<body>
<div class=""login-page"">
  <div class=""card"">
    <div class=""card-header"">
      <svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.5""><path d=""M17 7l-1.41 1.41L18.17 11H8v2h10.17l-2.58 2.58L17 17l5-5zM4 5h8V3H4c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h8v-2H4V5z""/></svg>
      <h1>Sign out</h1>
      <p>His.Hope Hospital Information System</p>
    </div>
    <div class=""card-body"">
      <p style=""margin-bottom:24px;font-size:15px;text-align:center;color:rgba(0,0,0,.87);line-height:1.6"">You are signed in as<br><strong>{encodedUserName}</strong>.</p>
      <form method=""post"" action=""/Account/Logout"">
        <input type=""hidden"" name=""returnUrl"" value=""{encodedReturnUrl}""/>
        <button type=""submit"" class=""btn""><svg viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M17 7l-1.41 1.41L18.17 11H8v2h10.17l-2.58 2.58L17 17l5-5zM4 5h8V3H4c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h8v-2H4V5z""/></svg>SIGN OUT</button>
      </form>
      <div style=""text-align:center;margin-top:16px"">
        <a href=""{encodedReturnUrl}"" style=""color:#3f51b5;text-decoration:none;font-size:14px;font-weight:500;letter-spacing:.25px"">Cancel</a>
      </div>
    </div>
  </div>
  <div class=""footer"">
    <strong>His.Hope</strong> v1.0 &bull; HIPAA-Compliant Security
  </div>
</div>
</body>
</html>";
}

app.MapPost("/Account/Login", async (HttpContext httpContext, SignInManager<User> signInManager, UserManager<User> userManager, OidcLoginCompletionService completion) =>
{
    var form = await httpContext.Request.ReadFormAsync();
    var email = form["email"].FirstOrDefault()?.Trim();
    var password = form["password"].FirstOrDefault();
    var returnUrl = form["returnUrl"].FirstOrDefault() ?? "/";

    if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        return Results.Redirect($"/Account/Login?error=invalid_credentials&returnUrl={System.Net.WebUtility.UrlEncode(returnUrl)}");

    // Determine if returnUrl is an absolute URL from this origin or a relative path
    if (!returnUrl.StartsWith('/'))
        returnUrl = "/";

    var user = await userManager.FindByEmailAsync(email);
    if (user == null)
        return Results.Redirect($"/Account/Login?error=invalid_credentials&returnUrl={System.Net.WebUtility.UrlEncode(returnUrl)}");

    var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: false);
    if (!result.Succeeded)
        return Results.Redirect($"/Account/Login?error=invalid_credentials&returnUrl={System.Net.WebUtility.UrlEncode(returnUrl)}");

    var completed = await completion.CompletePrimaryAsync(httpContext, user, returnUrl, ["pwd"]);
    return Results.Redirect(completed.RedirectUrl);
})
.AllowAnonymous();

app.MapPost("/Account/LdapLogin", async (HttpContext httpContext, LdapSyncService ldap,
    OidcLoginCompletionService completion, ExternalIdentityProviderRuntime runtime, CancellationToken ct) =>
{
    var form = await httpContext.Request.ReadFormAsync(ct);
    var userName = form["userName"].FirstOrDefault()?.Trim();
    var password = form["password"].FirstOrDefault();
    var returnUrl = form["returnUrl"].FirstOrDefault() ?? "/";

    if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password) || !returnUrl.StartsWith('/'))
        return Results.Redirect($"/Account/Login?error=invalid_credentials&returnUrl={WebUtility.UrlEncode(returnUrl.StartsWith('/') ? returnUrl : "/")}");

    var ldapConfig = await runtime.GetLdapAsync(ct);
    if (!ldapConfig.Enabled)
        return Results.Redirect($"/Account/Login?error=directory_unavailable&returnUrl={WebUtility.UrlEncode(returnUrl)}");

    var profile = await ldap.AuthenticateAndGetProfileAsync(userName, password, ct);
    if (profile is null || !profile.IsActive)
        return Results.Redirect($"/Account/Login?error=invalid_directory_credentials&returnUrl={WebUtility.UrlEncode(returnUrl)}");

    var user = await ldap.ProvisionUserAsync(profile, ct);
    if (!user.IsActive)
        return Results.Redirect($"/Account/Login?error=invalid_credentials&returnUrl={WebUtility.UrlEncode(returnUrl)}");

    var completed = await completion.CompletePrimaryAsync(httpContext, user, returnUrl, ["ldap"] , ct);
    return Results.Redirect(completed.RedirectUrl);
})
.AllowAnonymous()
.RequireRateLimiting("auth");

app.MapPost("/Account/ExternalLogin", async (HttpContext httpContext, SignInManager<User> signInManager) =>
{
    var form = await httpContext.Request.ReadFormAsync();
    var provider = form["provider"].FirstOrDefault();
    var returnUrl = form["returnUrl"].FirstOrDefault() ?? "/";

    if (string.IsNullOrEmpty(provider))
        return Results.Redirect($"/Account/Login?error=invalid_provider&returnUrl={System.Net.WebUtility.UrlEncode(returnUrl)}");

    var redirectUrl = $"/Account/ExternalLoginCallback?returnUrl={System.Net.WebUtility.UrlEncode(returnUrl)}";
    var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
    return Results.Challenge(properties, [provider]);
})
.AllowAnonymous();

// OIDC Logout endpoint (passthrough handler - Angular apps call oidcSecurityService.logoff())
app.MapGet("/connect/logout", async (HttpContext httpContext, SignInManager<User> signInManager) =>
{
    // Sign out the cookie
    await signInManager.SignOutAsync();

    var postLogoutUri = httpContext.Request.Query["post_logout_redirect_uri"].FirstOrDefault();
    if (!string.IsNullOrEmpty(postLogoutUri) && Uri.TryCreate(postLogoutUri, UriKind.Absolute, out _))
        return Results.Redirect(postLogoutUri);

    return Results.Redirect("/Account/Login");
}).AllowAnonymous();

// Logout endpoint (POST - server-rendered form)
app.MapPost("/Account/Logout", async (HttpContext httpContext, SignInManager<User> signInManager) =>
{
    var form = await httpContext.Request.ReadFormAsync();
    var returnUrl = form["returnUrl"].FirstOrDefault() ?? "/Account/Login";

    if (!returnUrl.StartsWith('/'))
        returnUrl = "/Account/Login";

    await signInManager.SignOutAsync();
    return Results.Redirect(returnUrl);
});
    }

    private static string NormalizeLocale(string value)
    {
        var candidate = value.Split(',', ';')[0].Trim();
        if (candidate.Equals("en", StringComparison.OrdinalIgnoreCase)) return "en-US";
        return candidate.Equals("en-US", StringComparison.OrdinalIgnoreCase) ? "en-US" : "vi-VN";
    }
}
