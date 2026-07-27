using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace His.Hope.Observability.OpenTelemetry;

public static class OpenTelemetryRegistration
{
    public static IServiceCollection AddHisHopeOpenTelemetryExporters(this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        var endpoint = configuration["OpenTelemetry:OtlpEndpoint"] ?? configuration["Otlp:Endpoint"];
        var builder = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName, serviceVersion: "1.0.0"))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation(options => options.RecordException = true)
                    .AddHttpClientInstrumentation(options => options.RecordException = true);
                if (Uri.TryCreate(endpoint, UriKind.Absolute, out var otlpEndpoint))
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = otlpEndpoint;
                        options.Protocol = OtlpExportProtocol.Grpc;
                    });
                }
            })
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("His.Hope.Identity")
                .AddMeter("His.Hope.Identity.Audit")
                .AddPrometheusExporter());

        return services;
    }

    public static IApplicationBuilder UseHisHopePrometheusEndpoint(this IApplicationBuilder app) =>
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
}
