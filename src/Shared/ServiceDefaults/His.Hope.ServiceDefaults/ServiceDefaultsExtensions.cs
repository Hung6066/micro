using His.Hope.AspNetCore;
using His.Hope.Infrastructure;
using His.Hope.Observability;
using His.Hope.Resilience;
using His.Hope.Validation;
using His.Hope.Secrets;
using His.Hope.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace His.Hope.ServiceDefaults;

public static class ServiceDefaultsExtensions
{
    /// <summary>
    /// Registers the complete golden-path host platform for a His.Hope service.
    /// Service-specific database, authorization and endpoint registrations remain
    /// explicit; cross-cutting infrastructure is registered exactly once here.
    /// </summary>
    public static IServiceCollection AddHisHopeServicePlatform(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        string? redisConnectionString = null)
    {
        services.AddHisHopeServiceDefaults(configuration, serviceName);
        services.AddHisHopeEnterpriseInfrastructure(
            configuration,
            serviceName,
            redisConnectionString
                ?? configuration.GetValue<string>(HisHopeConfigurationKeys.RedisConnectionString)
                ?? "localhost:6379");

        return services;
    }

    public static IServiceCollection AddHisHopeServiceDefaults(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        services.AddHisHopeAspNetCore();
        services.AddHisHopeVault(configuration);
        services.AddHisHopeExternalProviderBindings(configuration);
        services.AddHisHopeServiceToServiceAuthentication(configuration);
        services.Configure<HisHopeInternationalizationOptions>(
            configuration.GetSection(HisHopeInternationalizationOptions.SectionName));
        services.AddSingleton<IConfigureOptions<RequestLocalizationOptions>, HisHopeRequestLocalizationOptionsSetup>();
        services.AddObservability(options => options.ServiceName = serviceName);
        services.AddHisHopeResilience(configuration);
        // Services may compose this registration with another host-defaults package.
        // Do not add a named synthetic check here: duplicate named checks make the
        // health-check provider fail during startup. Concrete services register
        // readiness checks, while the live endpoint remains available with an
        // empty predicate when no service-specific liveness check exists.
        services.AddHealthChecks();

        return services;
    }

    public static IApplicationBuilder UseHisHopeServiceDefaults(this IApplicationBuilder app)
    {
        app.UseRequestLocalization();
        app.UseMiddleware<HisHopeInternationalizationMiddleware>();
        app.UseHisHopeAspNetCore();
        app.UseHisHopeValidationErrors();
        return app;
    }

    private sealed class HisHopeRequestLocalizationOptionsSetup(
        IOptions<HisHopeInternationalizationOptions> settings)
        : IConfigureOptions<RequestLocalizationOptions>
    {
        public void Configure(RequestLocalizationOptions options)
        {
            var cultures = settings.Value.SupportedCultures.Select(CultureInfo.GetCultureInfo).ToList();
            options.DefaultRequestCulture = new RequestCulture(settings.Value.DefaultCulture);
            options.SupportedCultures = cultures;
            options.SupportedUICultures = cultures;
            options.RequestCultureProviders.Clear();
            options.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());
        }
    }

    public static IEndpointRouteBuilder MapHisHopeHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(HisHopeHealthRoutes.Root, new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        }).AllowAnonymous();

        endpoints.MapHealthChecks(HisHopeHealthRoutes.Live, new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        }).AllowAnonymous();

        endpoints.MapHealthChecks(HisHopeHealthRoutes.Ready, new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        }).AllowAnonymous();

        return endpoints;
    }
}
