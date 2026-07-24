using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using His.Hope.Bff.Core.Authentication;
using His.Hope.IdentityService.Api.Configuration;
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
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using OpenIddictEntityFrameworkCore = OpenIddict.EntityFrameworkCore.Models;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
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

// Register SLO meter so OpenTelemetry collects custom identity metrics
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter("His.Hope.Identity"));

// Use Redis distributed cache for token blacklist + refresh token storage (shared across services).
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis")
        ?? builder.Configuration.GetValue<string>("Redis:ConnectionString")
        ?? "localhost:6379";
    options.InstanceName = "HisHope:";
});
builder.Services.AddSingleton<ICacheService, NoOpCacheService>();

// IdentityService user-management requests do not use distributed locks, so keep
// MediatR off Redis here to avoid an unnecessary IConnectionMultiplexer dependency.
builder.Services.AddSingleton<ILockManager, NoOpLockManager>();
builder.Services.AddSingleton<IUserSessionTracker, UserSessionTracker>();

builder.Services.AddIdentity<User, His.Hope.IdentityService.Domain.Entities.Role>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<IdentityDbContext>()
    .AddDefaultTokenProviders();

// SECURITY: JWT authentication with RSA public key validation
builder.Services.AddHisHopeJwtAuthentication(builder.Configuration);

// Policy scheme: use JWT for API calls (Authorization header), cookie for browser.
// This allows both cookie-based browser sessions and JWT-based API auth to coexist.
const string policyScheme = "HisHope.BrowserOrApi";
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = policyScheme;
    options.DefaultAuthenticateScheme = policyScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
})
.AddPolicyScheme(policyScheme, policyScheme, options =>
{
    options.ForwardDefaultSelector = context =>
    {
        // API calls with Bearer token → validate via OpenIddict (knows RSA keys)
        if (context.Request.Headers.ContainsKey("Authorization"))
            return OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        // Browser requests → cookie
        return IdentityConstants.ApplicationScheme;
    };
});

// SSO: share auth cookie across all localhost ports (8081, 8082, 8083, 4200, 4201, 4202)
builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme,
    options => options.Cookie.Domain = "localhost");

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
        config.PermitLimit = 120;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("audit-ingest", config =>
    {
        config.PermitLimit = 120;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("webhook", config =>
    {
        config.PermitLimit = 60;
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
builder.Services.AddHealthChecks()
    .AddCheck("vault-transit", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Vault not configured (dev mode)"), tags: new[] { "startup" });

// ─── OpenIddict OAuth2/OIDC Authorization Server ───
var oidcConfig = builder.Configuration.GetSection("OpenIddict");
var oidcSecurity = OidcSecurityConfiguration.Resolve(builder.Configuration, builder.Environment);

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<IdentityDbContext>();
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

        if (oidcSecurity.SigningKey is not null)
        {
            options.AddSigningKey(oidcSecurity.SigningKey);
        }
        else
        {
            // Development only. Production fails fast in OidcSecurityConfiguration.
            options.AddEphemeralSigningKey();
            options.AddEphemeralEncryptionKey();
        }

        options.DisableAccessTokenEncryption();

        var aspNetCoreOptions = options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableLogoutEndpointPassthrough()
               .EnableStatusCodePagesIntegration();

        if (oidcSecurity.AllowInsecureHttp)
        {
            aspNetCoreOptions.DisableTransportSecurityRequirement();
        }

        options.AddEventHandler<OpenIddictServerEvents.HandleConfigurationRequestContext>(builder =>
            builder.UseSingletonHandler<His.Hope.IdentityService.Api.Handlers.FixDiscoveryBaseUriHandler>()
                .SetOrder(int.MaxValue - 200_000) // Before AttachIssuer
                .SetType(OpenIddictServerHandlerType.Custom)
                .Build());

        options.AddEventHandler<OpenIddictServerEvents.HandleTokenRequestContext>(builder =>
            builder.UseScopedHandler<His.Hope.IdentityService.Application.OpenIddict.CustomPopulateTokenClaims>()
                .SetOrder(int.MaxValue - 100_000)
                .SetType(OpenIddictServerHandlerType.Custom)
                .Build());
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

// Trust forwarded headers from nginx/gateway so OpenIddict generates
// correct public endpoint URLs in the OIDC discovery document.
// SECURITY: In production, restrict KnownNetworks/KnownProxies to specific
// gateway IP ranges instead of allowing all.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
    ForwardLimit = null,
};
// DEV: Add Docker Compose bridge network range so the middleware trusts
// forwarded headers from the gateway container. Default KnownNetworks
// only includes loopback (127.0.0.0/8), which excludes Docker containers.
// NOTE: Clearing KnownNetworks/KnownProxies entirely does NOT trust all
// proxies — it trusts NONE, causing the middleware to skip all processing.
forwardedHeadersOptions.KnownNetworks.Add(
    new Microsoft.AspNetCore.HttpOverrides.IPNetwork(
        IPAddress.Parse("172.16.0.0"), 12));
// Also trust loopback for direct access (already in defaults, explicit to
// avoid any ambiguity):
forwardedHeadersOptions.KnownNetworks.Add(
    new Microsoft.AspNetCore.HttpOverrides.IPNetwork(
        IPAddress.Parse("127.0.0.0"), 8));
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

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
app.UseMiddleware<His.Hope.IdentityService.Api.Metrics.SloMiddleware>();
app.UseSerilogRequestLogging();
app.UseHisHopePrometheus();
app.UseCors();
app.UseRouting();

// Check cookie auth for OIDC authorize BEFORE OpenIddict processes it
// If no cookie → redirect to login (SSO + nice UX)
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/connect/authorize"))
    {
        var authResult = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!authResult.Succeeded)
        {
            var returnUrl = Uri.EscapeDataString(
                context.Request.PathBase + context.Request.Path + context.Request.QueryString);
            context.Response.Redirect($"/Account/Login?ReturnUrl={returnUrl}");
            return;
        }
    }
    await next();
});

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
            RefreshToken = result.RefreshToken,
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
.RequireRateLimiting("auth")
.AllowAnonymous();

auth.MapPost("/refresh", async (RefreshTokenRequest? request, IIdentityService identityService,
    IConnectionMultiplexer redis, HttpContext httpContext, CancellationToken ct) =>
{
    try
    {
        if (request is null || string.IsNullOrWhiteSpace(request.AccessToken) || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var session = await BffHelpers.GetSessionAsync(redis, httpContext);
            if (session is null || session.IsExpired)
                return Results.Unauthorized();

            var validationError = BffHelpers.ValidateSessionBinding(httpContext, session, requireCsrf: true);
            if (validationError is not null)
                return validationError;

            if (string.IsNullOrWhiteSpace(session.RefreshToken))
                return Results.Unauthorized();

            request = new RefreshTokenRequest(
                session.Jwt,
                session.RefreshToken,
                IpAddress: httpContext.Connection.RemoteIpAddress?.ToString());
        }

        var result = await identityService.RefreshTokenAsync(request, ct);

        var sessionId = httpContext.Request.Cookies["hishop_sid"];
        if (!string.IsNullOrWhiteSpace(sessionId))
            await BffHelpers.ReplaceSessionTokensAsync(redis, httpContext, sessionId, result);

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

auth.MapPost("/logout", async (IConnectionMultiplexer redis, HttpContext httpContext,
    IIdentityService identityService, IUserSessionTracker sessionTracker,
    ITokenBlacklistService tokenBlacklist, ILogger<Program> logger, CancellationToken ct) =>
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
                refreshToken = session.RefreshToken;
                userId = session.UserId;
            }
        }

        // Remove the current session immediately when present
        await db.KeyDeleteAsync($"session:{sessionId}");
    }

    // Fallback for SPA flow: extract userId from JWT Bearer token (no BFF session cookie)
    if (string.IsNullOrWhiteSpace(userId))
    {
        var authHeader = httpContext.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var jwt = authHeader["Bearer ".Length..];
            userId = JwtPayloadParser.ExtractUserIdFromJwtPayload(jwt);
            logger.LogDebug("Logout via JWT Bearer: UserId={UserId}", userId);
        }
    }

    // Revoke refresh token
    if (!string.IsNullOrWhiteSpace(refreshToken))
        await identityService.LogoutAsync(refreshToken, ct);

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
        Path = "/api",
        Expires = DateTimeOffset.UnixEpoch
    });
    httpContext.Response.Cookies.Append("hishop_csrf", "", new CookieOptions
    {
        HttpOnly = false,
        Secure = httpContext.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = DateTimeOffset.UnixEpoch
    });

    return Results.NoContent();
})
.WithDeprecationNotice()
.WithOpenApi()
.RequireRateLimiting("auth")
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

    var validationError = BffHelpers.ValidateSessionBinding(httpContext, session, requireCsrf: true);
    if (validationError is not null)
        return validationError;

    if (string.IsNullOrWhiteSpace(session.RefreshToken))
        return Results.Unauthorized();

    var refreshResult = await identityService.RefreshTokenAsync(
        new RefreshTokenRequest(
            session.Jwt,
            session.RefreshToken,
            IpAddress: httpContext.Connection.RemoteIpAddress?.ToString()), ct);

    session = session with
    {
        Jwt = refreshResult.AccessToken,
        RefreshToken = refreshResult.RefreshToken,
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
        Path = "/api",
        MaxAge = TimeSpan.FromHours(1)
    });
    httpContext.Response.Cookies.Append("hishop_csrf", session.CsrfToken, new CookieOptions
    {
        HttpOnly = false,
        Secure = httpContext.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        MaxAge = TimeSpan.FromHours(1)
    });

    return Results.Ok(new { refreshed = true });
})
.WithDeprecationNotice()
.WithOpenApi()
.RequireRateLimiting("auth");

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
audit.MapAuditLogEndpoints().RequireRateLimiting("audit-ingest");

// HR webhook (requires API key - validated via middleware or API key header)
var webhook = app.MapGroup("/api/v1");
webhook.MapHrWebhookEndpoints().RequireRateLimiting("webhook");

app.MapHealthChecks("/health").AllowAnonymous();
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

// ─── OIDC Authorization Endpoint (passthrough handler) ───
// OpenIddict validates the request and passes through. When the user is
// authenticated (cookie), we sign in with the OpenIddict scheme to generate
// the authorization code and redirect to the callback.
app.MapGet("/connect/authorize", async (
    HttpContext context,
    SignInManager<User> signInManager,
    UserManager<User> userManager,
    IOpenIddictScopeManager scopeManager) =>
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

    var principal = await signInManager.CreateUserPrincipalAsync(user);

    // Ensure sub claim is set (OpenIddict requires it on the principal directly)
    principal.SetClaim(OpenIddictConstants.Claims.Subject, user.Id.ToString());

    principal.SetScopes(request.GetScopes());

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

// ─── OIDC Login Page (server-rendered for authorization flow) ───
app.MapGet("/Account/Login", async (HttpContext httpContext, SignInManager<User> signInManager) =>
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
    var errorMessage = error == "invalid_credentials" ? "Invalid email or password." : (error ?? "");
    var hasError = !string.IsNullOrEmpty(error);
    var encodedReturnUrl = System.Net.WebUtility.HtmlEncode(returnUrl);

    var html = BuildLoginPage(hasError, errorMessage, encodedReturnUrl, externalProviders);

    // Remove restrictive CSP on response flush — login page CSS is self-contained (SVG, no external fonts)
    httpContext.Response.OnStarting(() =>
    {
        httpContext.Response.Headers.Remove("Content-Security-Policy");
        return Task.CompletedTask;
    });

    return Results.Content(html, "text/html; charset=utf-8");
})
.AllowAnonymous();

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

static string BuildLoginPage(bool hasError, string errorMessage, string encodedReturnUrl, List<string> externalProviders)
{
    var errorBlock = hasError
        ? $"<div class=\"mat-error\" role=\"alert\"><svg viewBox=\"0 0 24 24\" fill=\"currentColor\"><path d=\"M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z\"/></svg>{System.Net.WebUtility.HtmlEncode(errorMessage)}</div>"
        : "";

    var extBlock = "";
    if (externalProviders.Count > 0)
    {
        var btns = string.Join("\n", externalProviders.Select(p =>
            $"<form method=\"post\" action=\"/Account/ExternalLogin\"><input type=\"hidden\" name=\"provider\" value=\"{System.Net.WebUtility.HtmlEncode(p)}\" /><input type=\"hidden\" name=\"returnUrl\" value=\"{encodedReturnUrl}\" /><button type=\"submit\" class=\"mat-stroked-button\"><svg viewBox=\"0 0 24 24\" fill=\"currentColor\"><path d=\"M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z\"/></svg>{System.Net.WebUtility.HtmlEncode(p)}</button></form>"));
        extBlock = $"<div class=\"external-section\">{btns}</div>";
    }

    return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""utf-8""/>
<meta name=""viewport"" content=""width=device-width, initial-scale=1""/>
<title>Sign in — His.Hope HIS</title>
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
.mat-error{{
  display:flex;align-items:center;gap:10px;padding:12px 16px;margin-bottom:16px;
  background:#fbe9e7;color:#d32f2f;border-radius:4px;font-size:13px;
  border-left:3px solid #d32f2f;animation:shake .4s ease
}}
@keyframes shake{{
0%,100%{{transform:translateX(0)}}20%{{transform:translateX(-8px)}}40%{{transform:translateX(8px)}}60%{{transform:translateX(-4px)}}80%{{transform:translateX(4px)}}
}}
.mat-error svg{{width:20px;height:20px;flex-shrink:0}}
.field{{display:block;margin-bottom:20px;position:relative}}
.field label{{
  display:block;font-size:12px;font-weight:500;color:rgba(0,0,0,.6);
  margin-bottom:6px;letter-spacing:.4px;text-transform:uppercase
}}
.field-inner{{position:relative}}
.field-inner input{{
  width:100%;height:48px;padding:0 12px 0 44px;
  border:1px solid rgba(0,0,0,.12);border-radius:4px 4px 0 0;
  font-family:inherit;font-size:16px;font-weight:400;color:rgba(0,0,0,.87);
  background:rgba(0,0,0,.02);outline:none;
  transition:border-color .2s,background .2s,box-shadow .2s
}}
.field-inner input::placeholder{{color:rgba(0,0,0,.38)}}
.field-inner input:hover{{background:rgba(0,0,0,.04);border-color:rgba(0,0,0,.38)}}
.field-inner input:focus{{
  background:transparent;border-color:#3f51b5;border-width:2px;
  padding:0 11px 0 43px;box-shadow:inset 0 -2px 0 #3f51b5
}}
.field-inner input:focus+.underline{{transform:scaleX(1)}}
.underline{{
  position:absolute;bottom:0;left:0;right:0;height:2px;
  background:#3f51b5;transform:scaleX(0);
  transition:transform .2s cubic-bezier(.4,0,.2,1)
}}
.field-icon{{
  position:absolute;left:12px;top:50%;transform:translateY(-50%);
  pointer-events:none;transition:color .2s
}}
.field-icon svg{{width:20px;height:20px;color:rgba(0,0,0,.38);display:block}}
.field-inner input:focus~.field-icon svg{{color:#3f51b5}}
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
.external-section{{margin-top:16px;display:flex;flex-direction:column;gap:8px}}
.btn-outline{{
  display:inline-flex;align-items:center;justify-content:center;gap:8px;
  width:100%;min-height:36px;padding:0 16px;
  border:1px solid rgba(0,0,0,.12);border-radius:4px;
  font-family:inherit;font-size:14px;font-weight:500;
  color:rgba(0,0,0,.87);background:transparent;cursor:pointer;
  letter-spacing:.25px;transition:background .2s
}}
.btn-outline:hover{{background:rgba(0,0,0,.04)}}
.btn-outline svg{{width:20px;height:20px;color:#757575}}
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
      <svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.5""><path d=""M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-7 3c1.93 0 3.5 1.57 3.5 3.5S13.93 13 12 13s-3.5-1.57-3.5-3.5S10.07 6 12 6zm7 13H5v-.23c0-.62.28-1.2.76-1.58C7.47 15.82 9.64 15 12 15s4.53.82 6.24 2.19c.48.38.76.97.76 1.58V19z""/></svg>
      <h1>His.Hope</h1>
      <p>Hospital Information System</p>
    </div>
    <div class=""card-body"">
      {errorBlock}
      <form method=""post"" action=""/Account/Login"" autocomplete=""off"">
        <input type=""hidden"" name=""returnUrl"" value=""{encodedReturnUrl}""/>
        <div class=""field"">
          <label for=""email"">Email</label>
          <div class=""field-inner"">
            <input type=""email"" id=""email"" name=""email"" placeholder=""you@hospital.vn"" required autocomplete=""username""/>
            <span class=""field-icon""><svg viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M20 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4l-8 5-8-5V6l8 5 8-5v2z""/></svg></span>
            <span class=""underline""></span>
          </div>
        </div>
        <div class=""field"">
          <label for=""password"">Password</label>
          <div class=""field-inner"">
            <input type=""password"" id=""password"" name=""password"" placeholder=""Enter your password"" required autocomplete=""current-password""/>
            <span class=""field-icon""><svg viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M18 8h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zm-6 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zm3.1-9H8.9V6c0-1.71 1.39-3.1 3.1-3.1s3.1 1.39 3.1 3.1v2z""/></svg></span>
            <span class=""underline""></span>
          </div>
        </div>
        <button type=""submit"" class=""btn"">
          <svg viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M11 7L9.6 8.4l2.6 2.6H2v2h10.2l-2.6 2.6L11 17l5-5-5-5zm9 12h-8v2h8c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2h-8v2h8v14z""/></svg>
          SIGN IN
        </button>
      </form>
      {extBlock}
    </div>
  </div>
  <div class=""footer"">
    <strong>His.Hope</strong> v1.0 &bull; HIPAA-Compliant Security
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
  letter-spacing:.75px;text-transform:uppercase;cursor:pointer;text-decoration:none;
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
      <svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""1.5""><path d=""M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-7 3c1.93 0 3.5 1.57 3.5 3.5S13.93 13 12 13s-3.5-1.57-3.5-3.5S10.07 6 12 6zm7 13H5v-.23c0-.62.28-1.2.76-1.58C7.47 15.82 9.64 15 12 15s4.53.82 6.24 2.19c.48.38.76.97.76 1.58V19z""/></svg>
      <h1>His.Hope</h1>
      <p>Hospital Information System</p>
    </div>
    <div class=""card-body"">
      <p style=""margin-bottom:24px;font-size:15px;text-align:center;color:rgba(0,0,0,.87);line-height:1.6"">You are already signed in as<br><strong>{encodedUserName}</strong>.</p>
      <a href=""{encodedReturnUrl}"" class=""btn""><svg viewBox=""0 0 24 24"" fill=""currentColor""><path d=""M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z""/></svg>GO TO DASHBOARD</a>
      <div style=""text-align:center;margin-top:16px"">
        <a href=""/Account/Logout?returnUrl={encodedReturnUrl}"" style=""color:#3f51b5;text-decoration:none;font-size:14px;font-weight:500;letter-spacing:.25px"">SIGN OUT</a>
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

app.MapPost("/Account/Login", async (HttpContext httpContext, SignInManager<User> signInManager, UserManager<User> userManager) =>
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

    var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: false, lockoutOnFailure: false);
    if (!result.Succeeded)
        return Results.Redirect($"/Account/Login?error=invalid_credentials&returnUrl={System.Net.WebUtility.UrlEncode(returnUrl)}");

    return Results.Redirect(returnUrl);
})
.AllowAnonymous();

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

app.Run();

// ─── BFF Helpers ─────────────────────────────────────────────────────

file static class BffHelpers
{
    internal static async Task<SessionData?> GetSessionAsync(IConnectionMultiplexer redis, HttpContext httpContext)
    {
        var sessionId = httpContext.Request.Cookies["hishop_sid"];
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;

        var sessionJson = await redis.GetDatabase().StringGetAsync($"session:{sessionId}");
        return sessionJson.HasValue
            ? JsonSerializer.Deserialize<SessionData>(sessionJson!)
            : null;
    }

    internal static IResult? ValidateSessionBinding(HttpContext httpContext, SessionData session, bool requireCsrf)
    {
        var userAgentHash = ComputeSha256(httpContext.Request.Headers.UserAgent.ToString());
        if (!string.Equals(session.UserAgentHash, userAgentHash, StringComparison.Ordinal))
            return Results.Unauthorized();

        if (!requireCsrf)
            return null;

        var csrfHeader = httpContext.Request.Headers["X-CSRF-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(csrfHeader) ||
            !string.Equals(session.CsrfToken, csrfHeader, StringComparison.Ordinal))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        return null;
    }

    internal static async Task ReplaceSessionTokensAsync(
        IConnectionMultiplexer redis,
        HttpContext httpContext,
        string sessionId,
        TokenResponse tokenResponse)
    {
        var existing = await GetSessionAsync(redis, httpContext);
        if (existing is null)
            return;

        var next = existing with
        {
            Jwt = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
            ExpiresAt = tokenResponse.ExpiresAt,
            CsrfToken = Guid.NewGuid().ToString("N"),
            UserAgentHash = ComputeSha256(httpContext.Request.Headers.UserAgent.ToString()),
            IssuedAt = DateTimeOffset.UtcNow
        };

        await redis.GetDatabase().StringSetAsync(
            $"session:{sessionId}",
            JsonSerializer.Serialize(next),
            TimeSpan.FromHours(1));

        httpContext.Response.Cookies.Append("hishop_csrf", next.CsrfToken, new CookieOptions
        {
            HttpOnly = false,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            MaxAge = TimeSpan.FromHours(1)
        });
    }

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

// Helper: extract userId ("sub" claim) from JWT payload without full validation
file static class JwtPayloadParser
{
    public static string? ExtractUserIdFromJwtPayload(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return null;

            var payload = parts[1];
            // Base64Url decode (handle padding)
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("sub", out var sub)
                ? sub.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }
}
