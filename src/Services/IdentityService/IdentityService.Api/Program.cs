using System.Text;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("IdentityDb")));

builder.Services.AddIdentity<User, Role>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<IdentityDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<JwtTokenGenerator>();
builder.Services.AddScoped<IIdentityService, His.Hope.IdentityService.Infrastructure.Services.IdentityService>();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

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
    var userId = Guid.Parse(httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    var user = await identityService.GetUserByIdAsync(userId, ct);
    return Results.Ok(user);
})
.RequireAuthorization()
.WithOpenApi();

app.MapHealthChecks("/health");
app.Run();
