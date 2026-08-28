using His.Hope.AspNetCore.Authentication;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.ServiceDefaults;
using His.Hope.ManufacturingService.Infrastructure.Persistence;
using His.Hope.Authorization;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.Security;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHisHopeTenantPlacement(builder.Configuration);
builder.Services.AddManufacturingInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck<ManufacturingDatabaseHealthCheck>("manufacturing-db");
builder.Services.AddHisHopeServiceDefaults(builder.Configuration, "ManufacturingService");

var redis = RedisConnectionFactory.Connect(
    builder.Configuration.GetConnectionString("Redis")
        ?? builder.Configuration["Redis:ConnectionString"]
        ?? "localhost:6379",
    builder.Configuration);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

His.Hope.AspNetCore.Authentication.JwtAuthenticationExtensions.AddHisHopeJwtAuthentication(
    builder.Services,
    builder.Configuration);
builder.Services.AddHisHopeDpopValidation();
builder.Services.AddHisHopeAuthorization();

var app = builder.Build();

app.UseExceptionHandler();
app.UseHisHopeServiceDefaults();
app.UseDpopAuthorizationSchemeNormalization();
app.UseAuthentication();
app.UseDpopAccessTokenValidation();
app.UseAuthorization();
app.UseHisHopeTenantScope();
app.UseMiddleware<TenantRequestNormalizationMiddleware>();

app.ValidateHisHopeTenantPlacement();
app.Services.MigrateManufacturingDatabase();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();
app.MapManufacturingServiceEndpoints();

app.Run();

public partial class Program { }
