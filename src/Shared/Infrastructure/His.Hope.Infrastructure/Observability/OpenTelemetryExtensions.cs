using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace His.Hope.Infrastructure.Observability;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddHisHopeOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        var otlpEndpoint = configuration.GetValue("Otlp:Endpoint", "http://localhost:4317");
        var environment = configuration.GetValue("Environment", "development");

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: serviceName,
                    serviceVersion: "1.0.0",
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes([
                    new KeyValuePair<string, object>("deployment.environment", environment),
                ]))
            .WithTracing(tracing => tracing
                .AddSource(serviceName)
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = ctx =>
                        !ctx.Request.Path.StartsWithSegments("/health") &&
                        !ctx.Request.Path.StartsWithSegments("/swagger");
                    options.EnrichWithHttpRequest = (activity, request) =>
                    {
                        activity.SetTag("http.method", request.Method);
                        activity.SetTag("http.url", request.Path);
                    };
                    options.EnrichWithHttpResponse = (activity, response) =>
                    {
                        activity.SetTag("http.status_code", response.StatusCode);
                    };
                    options.RecordException = true;
                })
                .AddHttpClientInstrumentation(options =>
                {
                    options.EnrichWithHttpRequestMessage = (activity, request) =>
                    {
                        activity.SetTag("http.method", request.Method.Method);
                    };
                    options.RecordException = true;
                })
                .AddEntityFrameworkCoreInstrumentation(options =>
                {
                    options.SetDbStatementForText = true;
                    options.SetDbStatementForStoredProcedure = true;
                })
                .AddJaegerExporter(options =>
                {
                    options.AgentHost = configuration.GetValue("Otlp:Host", "localhost");
                    options.AgentPort = configuration.GetValue("Otlp:Port", 6831);
                })
                .SetSampler(new AlwaysOnSampler()))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddPrometheusExporter(options =>
                {
                    options.DisableTotalNameSuffixForCounters = true;
                    options.ScrapeResponseCacheDurationMilliseconds = 1000;
                }));

        return services;
    }

    public static IApplicationBuilder UseHisHopePrometheus(this IApplicationBuilder app) =>
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
}
