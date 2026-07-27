using His.Hope.AspNetCore;
using His.Hope.Observability;
using His.Hope.Resilience;
using His.Hope.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace His.Hope.ServiceDefaults;

public static class ServiceDefaultsExtensions
{
    public static IServiceCollection AddHisHopeServiceDefaults(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        services.AddHisHopeAspNetCore();
        services.AddObservability(options => options.ServiceName = serviceName);
        services.AddHisHopeResilience(configuration);
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live", "ready" });

        return services;
    }

    public static IApplicationBuilder UseHisHopeServiceDefaults(this IApplicationBuilder app)
    {
        app.UseHisHopeAspNetCore();
        app.UseHisHopeValidationErrors();
        return app;
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
