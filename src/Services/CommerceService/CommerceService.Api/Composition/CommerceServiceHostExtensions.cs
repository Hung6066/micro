using System.Security.Cryptography.X509Certificates;
using His.Hope.AspNetCore.Authentication;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.Authorization;
using His.Hope.CommerceService.Application;
using His.Hope.CommerceService.Api.Middleware;
using His.Hope.CommerceService.Infrastructure.Persistence;
using His.Hope.Configuration;
using His.Hope.Infrastructure.Middleware;
using His.Hope.Infrastructure.Security;
using His.Hope.ServiceDefaults;
using His.Hope.Secrets;
using Microsoft.AspNetCore.DataProtection;

namespace His.Hope.CommerceService.Api;

internal static class CommerceServiceHostExtensions
{
    public static IServiceCollection AddCommerceServiceHost(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddHisHopeTenantPlacement(configuration);
        services.AddHisHopeServicePlatform(configuration, "commerce-service");
        services.AddCommerceApplication();
        services.AddCommerceInfrastructure(configuration, environment);
        services.AddHealthChecks().AddCheck(
            "commerce-process",
            () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(),
            tags: ["live", "ready"]);
        services.AddSingleton<CommerceStore>();

        var dataProtection = services.AddDataProtection()
            .SetApplicationName("His.Hope.CommerceService")
            .PersistKeysToFileSystem(new DirectoryInfo(
                configuration["DataProtection:KeysPath"]
                    ?? "/var/lib/his-hope/commerce-data-protection-keys"));
        var certificatePath = configuration["DataProtection:CertificatePath"];
        if (!string.IsNullOrWhiteSpace(certificatePath))
        {
            if (!File.Exists(certificatePath))
                throw new InvalidOperationException(
                    $"DataProtection certificate was configured but not found: {certificatePath}");

            dataProtection.ProtectKeysWithCertificate(new X509Certificate2(
                certificatePath,
                configuration["DataProtection:CertificatePassword"]));
        }
        else if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "CommerceService requires DataProtection:CertificatePath outside Development.");
        }

        services.AddHisHopeDpopValidation();
        His.Hope.AspNetCore.Authentication.JwtAuthenticationExtensions.AddHisHopeJwtAuthentication(services, configuration);
        services.AddHisHopeAuthorization();
        services.AddAuthorizationBuilder().AddCommerceAuthorizationPolicies();
        return services;
    }

    public static WebApplication UseCommerceServiceHost(this WebApplication app)
    {
        app.UseGlobalExceptionHandler();
        app.UseHisHopeServiceDefaults();
        app.UseDpopAuthorizationSchemeNormalization();
        app.UseAuthentication();
        app.UseDpopAccessTokenValidation();
        app.UseAuthorization();
        app.UseCommerceSecurity();
        app.UseHisHopeTenantScope();
        return app;
    }
}
