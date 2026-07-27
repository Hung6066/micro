using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace His.Hope.AspNetCore.Health;

public static class HealthExtensions
{
    public static IHealthChecksBuilder AddHisHopeHealthChecks(this IServiceCollection services) =>
        services.AddHealthChecks();

    public static IEndpointConventionBuilder MapHisHopeHealthChecks(
        this IEndpointRouteBuilder endpoints,
        string path = "/health",
        Func<HealthCheckRegistration, bool>? predicate = null)
    {
        return endpoints.MapHealthChecks(path, new HealthCheckOptions
        {
            Predicate = predicate,
            ResponseWriter = HealthCheckResponseWriter.WriteAsync
        });
    }

    private static class HealthCheckResponseWriter
    {
        public static Task WriteAsync(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsJsonAsync(new
            {
                status = report.Status.ToString().ToLowerInvariant(),
                checks = report.Entries.ToDictionary(
                    entry => entry.Key,
                    entry => new { status = entry.Value.Status.ToString().ToLowerInvariant() })
            });
        }
    }
}
