using His.Hope.IdentityService.Api.Endpoints;
using His.Hope.IdentityService.Application;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.Infrastructure;
using His.Hope.Infrastructure.Audit;
using His.Hope.Infrastructure.Observability;
using His.Hope.Infrastructure.Security;
using His.Hope.Infrastructure.Security.Authorization;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("IdentityDb")));
builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());

builder.Services.AddHisHopeEnterpriseInfrastructure(
    builder.Configuration,
    "identity-service",
    builder.Configuration.GetValue("Redis:ConnectionString", "localhost:6379"));

builder.Services.AddIdentityCore<User>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.User.RequireUniqueEmail = true;
})
.AddRoles<Role>()
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
        listenOptions.UseHttps();
    });
    options.ListenAnyIP(5012, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    db.Database.EnsureCreated();
}

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

auth.MapPost("/login", async (LoginRequest request, IIdentityService identityService, CancellationToken ct) =>
{
    try
    {
        var result = await identityService.LoginAsync(request, ct);
        return Results.Ok(result);
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Problem(ex.Message, statusCode: 401);
    }
})
.WithOpenApi();

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
.WithOpenApi();

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
.WithOpenApi();

auth.MapPost("/logout", async (RefreshTokenRequest request, IIdentityService identityService, CancellationToken ct) =>
{
    await identityService.LogoutAsync(request.RefreshToken, ct);
    return Results.NoContent();
})
.RequireAuthorization()
.WithOpenApi();

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

// SECURITY: Token revocation endpoints
auth.MapTokenRevocationEndpoints();

// User management endpoints
var secured = app.MapGroup("/api/v1/auth").RequireAuthorization();
secured.MapUserEndpoints();
secured.MapRoleEndpoints();

var settings = app.MapGroup("/api/v1").RequireAuthorization();
settings.MapSettingsEndpoints();

var audit = app.MapGroup("/api/v1").RequireAuthorization();
audit.MapAuditLogEndpoints();

app.MapHealthChecks("/health");
app.Run();
