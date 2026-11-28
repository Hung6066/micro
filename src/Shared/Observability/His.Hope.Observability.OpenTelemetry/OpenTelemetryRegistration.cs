using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace His.Hope.Observability.OpenTelemetry;

public static class OpenTelemetryRegistration
{
    public static IServiceCollection AddHisHopeOpenTelemetryExporters(this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        var settings = TracingConfiguration.Read(configuration, serviceName);
        var httpClientFactory = OtlpTlsConfiguration.ReadHttpClientFactory(configuration, settings.Endpoint);
        var releaseSha = configuration["HIS_HOPE_RELEASE_SHA"]
            ?? Environment.GetEnvironmentVariable("HIS_HOPE_RELEASE_SHA")
            ?? "unknown";
        var releaseDigest = configuration["HIS_HOPE_RELEASE_DIGEST"]
            ?? Environment.GetEnvironmentVariable("HIS_HOPE_RELEASE_DIGEST")
            ?? "unknown";
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
        Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());
        services.Configure<LoggerFactoryOptions>(options =>
            options.ActivityTrackingOptions |= ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);

        // Do not use the automatic environment resource detector: callers
        // cannot supply trusted project/tenant identity through resource attrs.
        var resource = ResourceBuilder.CreateEmpty()
            .AddTelemetrySdk()
            .AddAttributes(settings.Attributes)
            .AddService(settings.ServiceName, serviceVersion: releaseSha)
            .AddAttributes([
                new KeyValuePair<string, object>("deployment.environment.name", settings.EnvironmentName),
                new KeyValuePair<string, object>("release.sha", releaseSha),
                new KeyValuePair<string, object>("release.digest", releaseDigest),
            ]);
        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing.SetResourceBuilder(resource)
                    .AddSource(serviceName, "His.Hope.MediatR")
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments("/health") &&
                            !context.Request.Path.StartsWithSegments("/metrics");
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.FilterHttpRequestMessage = request =>
                            request.RequestUri is not { } uri ||
                            (!new Microsoft.AspNetCore.Http.PathString(uri.AbsolutePath).StartsWithSegments("/health") &&
                             !new Microsoft.AspNetCore.Http.PathString(uri.AbsolutePath).StartsWithSegments("/metrics"));
                    });
                if (settings.Endpoint is not null)
                {
                    // Sampling is collector-owned, including remotely unsampled parents.
                    tracing.SetSampler(new AlwaysOnSampler());
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = settings.Endpoint;
                        options.Protocol = OtlpExportProtocol.Grpc;
                        if (httpClientFactory is not null)
                            options.HttpClientFactory = httpClientFactory;
                    });
                }
            })
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(resource)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("His.Hope.Identity")
                .AddMeter("His.Hope.Identity.Audit")
                .AddMeter("His.Hope.AspNetCore.Tenancy")
                .AddPrometheusExporter());

        return services;
    }

    public static IApplicationBuilder UseHisHopePrometheusEndpoint(this IApplicationBuilder app) =>
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
}
