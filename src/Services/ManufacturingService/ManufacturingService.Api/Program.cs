using System.Security.Cryptography.X509Certificates;
using His.Hope.AspNetCore.Authentication;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.ServiceDefaults;
using His.Hope.ManufacturingService.Infrastructure.Persistence;
using His.Hope.Authorization;
using His.Hope.Infrastructure.Security;
using His.Hope.Infrastructure.Middleware;
using His.Hope.Configuration;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHisHopeTenantPlacement(builder.Configuration);
builder.Services.AddManufacturingInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck<ManufacturingDatabaseHealthCheck>("manufacturing-db");
builder.Services.AddHisHopeServicePlatform(builder.Configuration, "manufacturing-service");

// Keep antiforgery/session-protected payloads valid across container replacement.
// Production deployments must provide the certificate through the secret provider;
// local Development may use the persisted volume without certificate wrapping.
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("His.Hope.ManufacturingService")
    .PersistKeysToFileSystem(new DirectoryInfo(
        builder.Configuration["DataProtection:KeysPath"]
            ?? "/var/lib/his-hope/manufacturing-data-protection-keys"));
var dataProtectionCertificatePath = builder.Configuration["DataProtection:CertificatePath"];
if (!string.IsNullOrWhiteSpace(dataProtectionCertificatePath))
{
    if (!File.Exists(dataProtectionCertificatePath))
    {
        throw new InvalidOperationException(
            $"DataProtection certificate was configured but not found: {dataProtectionCertificatePath}");
    }

    dataProtection.ProtectKeysWithCertificate(new X509Certificate2(
        dataProtectionCertificatePath,
        builder.Configuration["DataProtection:CertificatePassword"]));
}
else if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "ManufacturingService requires DataProtection:CertificatePath outside Development.");
}

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
var runManufacturingMigrations = builder.Configuration.GetValue("Persistence:RunMigrationsOnStartup", false) ||
    builder.Configuration.GetValue("Persistence:MigrationOnly", false);
if (runManufacturingMigrations)
{
    app.Services.MigrateManufacturingDatabase();
}
else if (!app.Environment.IsDevelopment() &&
         !string.Equals(app.Environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "ManufacturingService requires Persistence:RunMigrationsOnStartup or Persistence:MigrationOnly outside Development.");
}

if (builder.Configuration.GetValue("Persistence:MigrationOnly", false))
{
    return;
}

app.MapHisHopeHealthEndpoints();
app.MapManufacturingServiceEndpoints();

app.Run();

public partial class Program { }
