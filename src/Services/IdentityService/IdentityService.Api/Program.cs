using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using His.Hope.Bff.Core.Authentication;
using His.Hope.IdentityService.Api.Endpoints;
using His.Hope.IdentityService.Api.Services;
using His.Hope.IdentityService.Application;
using His.Hope.IdentityService.Api.Services;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Application.Services;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.Infrastructure;
using His.Hope.Infrastructure.Audit;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.Middleware;
using His.Hope.Infrastructure.Observability;
using His.Hope.Infrastructure.Locking;
using His.Hope.Infrastructure.Security;
using His.Hope.Infrastructure.Security.Authorization;
using MediatR;
using OpenIddictEntityFrameworkCore = OpenIddict.EntityFrameworkCore.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration)
                .Enrich.WithProperty("service", "identity-service"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("IdentityDb"))
        .UseSnakeCaseNamingConvention());
builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());

builder.Services.AddHisHopeEnterpriseInfrastructure(
    builder.Configuration,
    "identity-service",
    builder.Configuration.GetValue("Redis:ConnectionString", "localhost:6379"));

// Use in-memory distributed cache for token blacklist + refresh token storage in this service.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSingleton<ICacheService, NoOpCacheService>();

// IdentityService user-management requests do not use distributed locks, so keep
// MediatR off Redis here to avoid an unnecessary IConnectionMultiplexer dependency.
builder.Services.AddSingleton<ILockManager, NoOpLockManager>();

builder.Services.AddIdentityCore<User>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.User.RequireUniqueEmail = true;
})
.AddRoles<His.Hope.IdentityService.Domain.Entities.Role>()
.AddEntityFrameworkStores<IdentityDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<SignInManager<User>>();

// SECURITY: JWT authentication with RSA public key validation
builder.Services.AddHisHopeJwtAuthentication(builder.Configuration);

// ─── External Identity Providers (Federation) ───
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
            options.SaveTokens = true;
            options.SignInScheme = IdentityConstants.ExternalScheme;
        });
}

var msClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
var msClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
if (!string.IsNullOrEmpty(msClientId) && !string.IsNullOrEmpty(msClientSecret))
{
    builder.Services.AddAuthentication()
        .AddMicrosoftAccount(options =>
        {
            options.ClientId = msClientId;
            options.ClientSecret = msClientSecret;
            options.SaveTokens = true;
            options.SignInScheme = IdentityConstants.ExternalScheme;
        });
}

// SECURITY: Token blacklist service for JWT revocation
builder.Services.AddHisHopeTokenBlacklist();

// SECURITY: Register permission-based authorization policies
builder.Services.AddHisHopeAuthorization();
builder.Services.AddScoped<JwtTokenGenerator>();
builder.Services.AddScoped<IIdentityService, His.Hope.IdentityService.Infrastructure.Services.IdentityService>();
builder.Services.AddScoped<TotpService>();
builder.Services.AddScoped<RecoveryCodeService>();
builder.Services.AddScoped<IdentityBrokerService>();
builder.Services.AddScoped<BulkUserImportService>();

// CORS for dashboard app (separate origin)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:8082", "http://localhost:4201")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Configure rate limiting specifically for auth endpoints
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", config =>
    {
        config.PermitLimit = 30;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 0;
    });
});

// SECURITY: Redis-backed refresh token store (replaces in-memory ConcurrentDictionary)
builder.Services.AddSingleton<RedisRefreshTokenStore>();

// SECURITY: Binds tokens to (user_id, ip_hash, client_id) to prevent cross-IP replay attacks
builder.Services.AddSingleton<TokenBindingService>();

builder.Services.AddIdentityApplication();

// LDAP Sync service (disabled by default)
builder.Services.AddScoped<LdapSyncService>();
builder.Services.AddHostedService<LdapBackgroundService>();

// gRPC services
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});
builder.Services.AddGrpcReflection();

// ─── Vault transit signing (development: ephemeral RSA) ───
builder.Services.AddSingleton<IVaultKeyProvider, VaultKeyService>();
builder.Services.AddSingleton<VaultClientSecretStore>();
builder.Services.AddSingleton<VaultClientSecretStore>();
builder.Services.AddHealthChecks().AddCheck<VaultHealthCheck>("vault-transit", tags: new[] { "startup" });

// ─── OpenIddict OAuth2/OIDC Authorization Server ───
var oidcConfig = builder.Configuration.GetSection("OpenIddict");

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<IdentityDbContext>()
               .ReplaceDefaultEntities<Guid>();
    })
    .AddServer(options =>
    {
        options.SetIssuer(new Uri(oidcConfig["Issuer"]!));

        options.SetAuthorizationEndpointUris("/connect/authorize");
        options.SetTokenEndpointUris("/connect/token");
        options.SetLogoutEndpointUris("/connect/logout");
        options.SetIntrospectionEndpointUris("/connect/introspect");

        options.AllowAuthorizationCodeFlow()
               .AllowRefreshTokenFlow()
               .AllowClientCredentialsFlow()
               .RequireProofKeyForCodeExchange();

        options.SetAccessTokenLifetime(TimeSpan.Parse(oidcConfig["AccessTokenLifetime"]!));
        options.SetRefreshTokenLifetime(TimeSpan.Parse(oidcConfig["RefreshTokenLifetime"]!));
        options.SetAuthorizationCodeLifetime(TimeSpan.Parse(oidcConfig["AuthorizationCodeLifetime"]!));

        // DEVELOPMENT: Add ephemeral RSA signing key
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var devKey = new RsaSecurityKey(rsa) { KeyId = "dev-jwt-signing" };
        options.AddSigningKey(devKey);

        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough()
               .EnableLogoutEndpointPassthrough()
               .EnableStatusCodePagesIntegration();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// PHI Audit Service Configuration (HIPAA 164.312(b))
var defaultAuditDescriptor = builder.Services.FirstOrDefault(
    sd => sd.ServiceType == typeof(His.Hope.Infrastructure.Audit.IAuditService));
if (defaultAuditDescriptor != null)
    builder.Services.Remove(defaultAuditDescriptor);

builder.Services.AddSingleton<DatabaseAuditService>();

builder.Services.AddSingleton<His.Hope.Infrastructure.Audit.IAuditService>(sp =>
{
    var serilogAudit = new His.Hope.Infrastructure.Audit.AuditService();
    var dbAudit = sp.GetRequiredService<DatabaseAuditService>();
    return new CompositeAuditService(serilogAudit, dbAudit);
});

builder.Services.AddHealthChecks();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5001, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
    options.ListenAnyIP(5012, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
});

var app = builder.Build();

app.UseCorrelationId();
app.UseGlobalExceptionHandler();

// SECURITY: Seed identity database with permissions, roles, and admin user
His.Hope.IdentityService.Infrastructure.Persistence.IdentityDbInitializer.Initialize(
    app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSecurityHeaders();
app.UseRateLimiting();
app.UseSerilogRequestLogging();
app.UseHisHopePrometheus();
app.UseCors();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UsePhiAudit();

// Auth endpoints
var auth = app.MapGroup("/api/v1/auth");

auth.MapPost("/login", async (LoginRequest request, IIdentityService identityService,
    IConnectionMultiplexer redis, HttpContext httpContext, CancellationToken ct) =>
{
    try
    {
        var result = await identityService.LoginAsync(request, ct);

        // BFF: Create Redis session and set cookies (dual-mode: cookie + Bearer)
        var permissions = BffHelpers.ExtractPermissionsFromJwt(result.AccessToken);
        var sessionId = Guid.NewGuid().ToString("N");
        var csrfToken = Guid.NewGuid().ToString("N");
        var sessionData = new SessionData
        {
            UserId = result.User.Id.ToString(),
            Jwt = result.AccessToken,
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
            Path = "/api",
            MaxAge = TimeSpan.FromHours(1)
        });

        httpContext.Response.Cookies.Append("hishop_csrf", csrfToken, new CookieOptions
        {
            HttpOnly = false,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            MaxAge = TimeSpan.FromHours(1)
        });

        return Results.Ok(result);
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

auth.MapPost("/refresh", async (RefreshTokenRequest request, IIdentityService identityService, CancellationToken ct) =>
{
    try
    {
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
    IIdentityService identityService, CancellationToken ct) =>
{
    await identityService.LogoutAsync(string.Empty, ct);

    var sessionId = httpContext.Request.Cookies["hishop_sid"];
    if (!string.IsNullOrEmpty(sessionId))
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync($"session:{sessionId}");
    }

    httpContext.Response.Cookies.Append("hishop_sid", "", new CookieOptions
    {
        HttpOnly = true, Secure = httpContext.Request.IsHttps, SameSite = SameSiteMode.Lax,
        Path = "/api", Expires = DateTimeOffset.UnixEpoch
    });
    httpContext.Response.Cookies.Append("hishop_csrf", "", new CookieOptions
    {
        HttpOnly = false, Secure = httpContext.Request.IsHttps, SameSite = SameSiteMode.Strict,
        Path = "/", Expires = DateTimeOffset.UnixEpoch
    });

    return Results.NoContent();
})
.WithDeprecationNotice()
.WithOpenApi()
.AllowAnonymous();

// BFF internal: exchange session ID for new JWT (transparent refresh)
auth.MapPost("/internal/refresh", async (IConnectionMultiplexer redis, HttpContext httpContext,
    IIdentityService identityService, CancellationToken ct) =>
{
    var sessionId = httpContext.Request.Cookies["hishop_sid"];
    if (string.IsNullOrEmpty(sessionId))
        return Results.BadRequest(new { error = "No session cookie" });

    var db = redis.GetDatabase();
    var sessionJson = await db.StringGetAsync($"session:{sessionId}");
    if (!sessionJson.HasValue)
        return Results.Unauthorized();

    var session = JsonSerializer.Deserialize<SessionData>(sessionJson!);
    if (session is null || session.IsExpired)
        return Results.Unauthorized();

    var refreshResult = await identityService.RefreshTokenAsync(
        new RefreshTokenRequest(session.Jwt, ""), ct);

    session = session with
    {
        Jwt = refreshResult.AccessToken,
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
        HttpOnly = true, Secure = httpContext.Request.IsHttps, SameSite = SameSiteMode.Lax,
        Path = "/api", MaxAge = TimeSpan.FromHours(1)
    });
    httpContext.Response.Cookies.Append("hishop_csrf", session.CsrfToken, new CookieOptions
    {
        HttpOnly = false, Secure = httpContext.Request.IsHttps, SameSite = SameSiteMode.Strict,
        Path = "/", MaxAge = TimeSpan.FromHours(1)
    });

    return Results.Ok(new { refreshed = true });
})
.WithDeprecationNotice()
.WithOpenApi();

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
    SignInManager<User> signInManager, UserManager<User> userManager, CancellationToken ct) =>
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

    await signInManager.SignInAsync(user, isPersistent: false);

    var returnUrl = httpContext.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
    return Results.Redirect(returnUrl);
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
secured.MapUserEndpoints();
secured.MapRoleEndpoints();

// Admin API endpoints (for frontend admin module)
var admin = app.MapGroup("/api/v1/admin").RequireAuthorization();
admin.MapUserEndpoints();
admin.MapRoleEndpoints();
admin.MapSettingsEndpoints();
admin.MapAuditLogEndpoints();
admin.MapGroup("/clients").MapClientEndpoints();
admin.MapBulkImportEndpoints();
admin.MapGroup("/consents").RequireAuthorization("Permission:admin.users.read").MapGet("/", async (IdentityDbContext db, CancellationToken ct) =>
{
    var totalConsents = await db.ClientConsents.CountAsync(ct);
    var activeConsents = await db.ClientConsents.CountAsync(c => c.IsActive, ct);
    var consents = await db.ClientConsents
        .Where(c => c.IsActive)
        .OrderByDescending(c => c.GrantedAt)
        .Take(20)
        .Select(c => new { c.Id, c.UserId, c.ClientId, Scopes = c.Scopes, c.GrantedAt, c.ExpiresAt })
        .ToListAsync(ct);
    return Results.Ok(new { totalConsents, activeConsents, recentConsents = consents });
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

app.MapHealthChecks("/health").AllowAnonymous();

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

// ─── SCIM v2 Provisioning API (RFC 7643/7644) ───
app.MapScimEndpoints();

app.Run();

// ─── BFF Helpers ─────────────────────────────────────────────────────

file static class BffHelpers
{
    internal static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input ?? ""));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    internal static string[] ExtractPermissionsFromJwt(string jwt)
    {
        try
        {
            var payload = jwt.Split('.')[1];
            var base64 = payload.Replace('-', '+').Replace('_', '/');
            var padded = base64.PadRight(((base64.Length + 3) / 4) * 4, '=');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("permissions", out var permProp))
            {
                var value = permProp.GetString();
                if (!string.IsNullOrEmpty(value))
                    return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
        }
        catch { }
        return [];
    }
}

file sealed class NoOpLockManager : ILockManager
{
    public Task<IDistributedLock?> AcquireAsync(string key, TimeSpan? ttl = null, CancellationToken ct = default)
        => Task.FromResult<IDistributedLock?>(null);
}

file sealed class NoOpCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class => Task.FromResult<T?>(null);
    public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default) where T : class => factory();
    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class => Task.CompletedTask;
    public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) => Task.CompletedTask;
}

file sealed record PermissionCheckRequest(string? Permission);

// DEPRECATED: Legacy auth endpoints maintained for backward compatibility.
// Migrate to OIDC /connect/authorize and /connect/token.
// These will be removed in Release N+2.
file static class LegacyEndpointFilter
{
    public static RouteHandlerBuilder WithDeprecationNotice(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter(async (ctx, next) =>
        {
            ctx.HttpContext.Response.Headers["Deprecation"] = "true";
            ctx.HttpContext.Response.Headers["Sunset"] = "Sat, 01 Jan 2028 00:00:00 GMT";
            ctx.HttpContext.Response.Headers["Link"] = "</connect/authorize>; rel=\"successor-version\"";
            return await next(ctx);
        });
    }
}
