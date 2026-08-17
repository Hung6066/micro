using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Exporter;
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
        var otlpEndpoint = configuration.GetValue<string>("Otlp:Endpoint") ?? "http://localhost:4317";
        var environment = configuration.GetValue<string>("Environment") ?? "development";
        var releaseSha = configuration["HIS_HOPE_RELEASE_SHA"]
            ?? Environment.GetEnvironmentVariable("HIS_HOPE_RELEASE_SHA")
            ?? "unknown";
        var releaseDigest = configuration["HIS_HOPE_RELEASE_DIGEST"]
            ?? Environment.GetEnvironmentVariable("HIS_HOPE_RELEASE_DIGEST")
            ?? "unknown";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: serviceName,
                    serviceVersion: releaseSha,
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes([
                    new KeyValuePair<string, object>("deployment.environment", environment),
                    new KeyValuePair<string, object>("release.sha", releaseSha),
                    new KeyValuePair<string, object>("release.digest", releaseDigest),
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
                        activity.SetTag("correlation.id", request.Headers["X-Correlation-Id"].FirstOrDefault() ?? "unknown");
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
                .AddGrpcClientInstrumentation(options =>
                {
                    options.SuppressDownstreamInstrumentation = true;
                })
                .AddEntityFrameworkCoreInstrumentation(options =>
                {
                    options.SetDbStatementForText = false;
                    options.SetDbStatementForStoredProcedure = true;
                })
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                    options.Protocol = OtlpExportProtocol.Grpc;
                })
                .SetSampler(new AlwaysOnSampler()))
            .WithMetrics(metrics => metrics
                .AddMeter("His.Hope.Infrastructure.Caching")
                .AddMeter("His.Hope.Outbox")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddPrometheusExporter(options =>
                {
                    options.ScrapeResponseCacheDurationMilliseconds = 1000;
                }));

        return services;
    }

    public static IApplicationBuilder UseHisHopePrometheus(this IApplicationBuilder app) =>
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
}
