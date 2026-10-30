using His.Hope.AspNetCore.Authentication;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.ServiceDefaults;
using His.Hope.ManufacturingService.Infrastructure.Persistence;
using His.Hope.Authorization;
using His.Hope.Infrastructure.Security;
using His.Hope.Infrastructure.Middleware;
using His.Hope.Infrastructure.Configuration;
using His.Hope.Configuration;

His.Hope.Configuration.PlatformPki.Materialize();
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.ApplyOpenshipBindings("ManufacturingDb");

builder.Services.AddHisHopeTenantPlacement(builder.Configuration);
builder.Services.AddManufacturingInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck<ManufacturingDatabaseHealthCheck>("manufacturing-db");
builder.Services.AddHisHopeServicePlatform(builder.Configuration, "manufacturing-service");

builder.Services.AddHisHopeRedisDataProtection(
    builder.Configuration,
    builder.Environment,
    "His.Hope.ManufacturingService",
    "HisHope:ManufacturingService:DataProtection:Keys");

His.Hope.AspNetCore.Authentication.JwtAuthenticationExtensions.AddHisHopeJwtAuthentication(
    builder.Services,
    builder.Configuration);
builder.Services.AddHisHopeDpopValidation();
builder.Services.AddHisHopeAuthorization();

var app = builder.Build();

app.UseGlobalExceptionHandler();
app.UseHisHopeServiceDefaults();
app.UseDpopAuthorizationSchemeNormalization();
app.UseAuthentication();
app.UseDpopAccessTokenValidation();
app.UseAuthorization();
app.UseHisHopeTenantScope();
app.UseMiddleware<TenantRequestNormalizationMiddleware>();

app.ValidateHisHopeTenantPlacement();
var runManufacturingMigrations = PersistenceMigrationStartupPolicy.ShouldRunMigrations(
    builder.Configuration,
    app.Environment,
    "ManufacturingService");
if (runManufacturingMigrations)
{
    app.Services.MigrateManufacturingDatabase();
}

if (builder.Configuration.GetValue("Persistence:MigrationOnly", false))
{
    return;
}

app.MapHisHopeHealthEndpoints();
app.MapManufacturingServiceEndpoints();

app.Run();

public partial class Program { }
