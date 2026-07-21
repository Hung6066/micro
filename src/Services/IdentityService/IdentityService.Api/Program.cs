using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using His.Hope.Bff.Core.Authentication;
using His.Hope.IdentityService.Api.Endpoints;
using His.Hope.IdentityService.Application;
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
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

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

// SECURITY: JWT authentication with RSA public key validation
builder.Services.AddHisHopeJwtAuthentication(builder.Configuration);

// SECURITY: Token blacklist service for JWT revocation
builder.Services.AddHisHopeTokenBlacklist();

// SECURITY: Register permission-based authorization policies
builder.Services.AddHisHopeAuthorization();
builder.Services.AddScoped<JwtTokenGenerator>();
builder.Services.AddScoped<IIdentityService, His.Hope.IdentityService.Infrastructure.Services.IdentityService>();
builder.Services.AddScoped<TotpService>();
builder.Services.AddScoped<RecoveryCodeService>();

// SECURITY: Redis-backed refresh token store (replaces in-memory ConcurrentDictionary)
builder.Services.AddSingleton<RedisRefreshTokenStore>();

builder.Services.AddIdentityApplication();

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
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/api",
            MaxAge = TimeSpan.FromHours(1)
        });

        httpContext.Response.Cookies.Append("hishop_csrf", csrfToken, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api",
            MaxAge = TimeSpan.FromHours(1)
        });

        return Results.Ok(result);
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Problem(ex.Message, statusCode: 401);
    }
})
.WithOpenApi()
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
        HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax,
        Path = "/api", Expires = DateTimeOffset.UnixEpoch
    });
    httpContext.Response.Cookies.Append("hishop_csrf", "", new CookieOptions
    {
        HttpOnly = false, Secure = true, SameSite = SameSiteMode.Strict,
        Path = "/api", Expires = DateTimeOffset.UnixEpoch
    });

    return Results.NoContent();
})
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
        HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax,
        Path = "/api", MaxAge = TimeSpan.FromHours(1)
    });
    httpContext.Response.Cookies.Append("hishop_csrf", session.CsrfToken, new CookieOptions
    {
        HttpOnly = false, Secure = true, SameSite = SameSiteMode.Strict,
        Path = "/api", MaxAge = TimeSpan.FromHours(1)
    });

    return Results.Ok(new { refreshed = true });
})
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

// MFA endpoints
auth.MapMfaEndpoints();

// SECURITY: Token revocation endpoints
auth.MapTokenRevocationEndpoints();

// User management endpoints
var secured = app.MapGroup("/api/v1/auth").RequireAuthorization();
secured.MapUserEndpoints();
secured.MapRoleEndpoints();

// Admin API endpoints (for frontend admin module)
var admin = app.MapGroup("/api/v1/admin").RequireAuthorization();
admin.MapUserEndpoints();
admin.MapRoleEndpoints();
admin.MapGet("/dashboard", async (IdentityDbContext db, CancellationToken ct) =>
{
    var totalUsers = await db.Users.CountAsync(ct);
    var activeUsers = await db.Users.CountAsync(u => u.IsActive, ct);
    var totalRoles = await db.Roles.CountAsync(ct);
    return Results.Ok(new { totalUsers, activeUsers, totalRoles });
}).RequireAuthorization("Permission:admin.users.read");

var settings = app.MapGroup("/api/v1").RequireAuthorization();
settings.MapSettingsEndpoints();

var audit = app.MapGroup("/api/v1").RequireAuthorization();
audit.MapAuditLogEndpoints();

app.MapHealthChecks("/health").AllowAnonymous();
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
