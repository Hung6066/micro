using His.Hope.AspNetCore;
using His.Hope.Observability;
using His.Hope.Resilience;
using His.Hope.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace His.Hope.ServiceDefaults;

public static class ServiceDefaultsExtensions
{
    public static IServiceCollection AddHisHopeServiceDefaults(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        services.AddHisHopeAspNetCore();
        services.Configure<HisHopeInternationalizationOptions>(
            configuration.GetSection(HisHopeInternationalizationOptions.SectionName));
        services.AddSingleton<IConfigureOptions<RequestLocalizationOptions>, HisHopeRequestLocalizationOptionsSetup>();
        services.AddObservability(options => options.ServiceName = serviceName);
        services.AddHisHopeResilience(configuration);
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live", "ready" });

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
        endpoints.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        }).AllowAnonymous();

        endpoints.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        }).AllowAnonymous();

        return endpoints;
    }
}
