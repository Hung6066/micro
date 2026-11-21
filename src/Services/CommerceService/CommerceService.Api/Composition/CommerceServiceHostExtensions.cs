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

        services.AddHisHopeRedisDataProtection(
            configuration,
            environment,
            "His.Hope.CommerceService",
            "HisHope:CommerceService:DataProtection:Keys");

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
