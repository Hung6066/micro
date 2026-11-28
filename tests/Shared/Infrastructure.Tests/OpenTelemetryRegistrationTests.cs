using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using His.Hope.Observability.OpenTelemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Core;
using Serilog.Events;

namespace His.Hope.Infrastructure.Tests;

[CollectionDefinition("OpenTelemetry", DisableParallelization = true)]
public sealed class OpenTelemetryCollection;

[Collection("OpenTelemetry")]
public sealed class OpenTelemetryRegistrationTests
{
    [Theory]
    [InlineData("OTEL_EXPORTER_OTLP_ENDPOINT")]
    [InlineData("OpenTelemetry:OtlpEndpoint")]
    [InlineData("Otlp:Endpoint")]
    public void EndpointAliases_ConfigureGrpcExporter(string key)
    {
        var services = Register(new() { [key] = "https://collector.example:4317" });
        var collector = new CollectorStub();
        services.Configure<OtlpExporterOptions>(options => options.HttpClientFactory = () => new HttpClient(collector));
        using var provider = services.BuildServiceProvider();
        var tracer = provider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource("identity-service");
        source.StartActivity("request")!.Dispose();
        Assert.True(tracer.ForceFlush());

        Assert.Equal("collector.example", collector.RequestUri!.Host);
        Assert.Equal("https", collector.RequestUri.Scheme);
        Assert.Equal(4317, collector.RequestUri.Port);
        Assert.Equal("/opentelemetry.proto.collector.trace.v1.TraceService/Export", collector.RequestUri.AbsolutePath);
        Assert.Equal(HttpVersion.Version20, collector.RequestVersion);
    }

    [Fact]
    public void StandardEndpoint_TakesPrecedenceOverLegacyAliases()
    {
        var settings = TracingConfiguration.Read(Config(new()
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "https://standard.example:4317",
            ["OpenTelemetry:OtlpEndpoint"] = "https://legacy.example:4317",
            ["Otlp:Endpoint"] = "https://oldest.example:4317",
        }), "identity-service");

        Assert.Equal("standard.example", settings.Endpoint!.Host);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a URL")]
    [InlineData("/relative")]
    [InlineData("ftp://collector.example")]
    [InlineData("https://user:password@collector.example")]
    [InlineData("https://collector.example?token=secret")]
    [InlineData("https://collector.example#fragment")]
    public void BadSuppliedEndpoint_FailsWithoutFallback(string endpoint)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Register(new()
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint,
            ["Otlp:Endpoint"] = "http://localhost:4317"
        }));

        Assert.Contains("OTEL_EXPORTER_OTLP_ENDPOINT", exception.Message);
        Assert.DoesNotContain("password", exception.Message);
        Assert.DoesNotContain("secret", exception.Message);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("=value")]
    [InlineData("key=")]
    [InlineData("key=value,")]
    public void MalformedResourceAttributes_FailClearly(string attributes)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Register(new() { ["OTEL_RESOURCE_ATTRIBUTES"] = attributes }));

        Assert.Contains("OTEL_RESOURCE_ATTRIBUTES", exception.Message);
    }

    [Fact]
    public void Resource_EnforcesIdentityAndDoesNotTrustTenantAttribution()
    {
        var services = Register(new()
        {
            ["OTEL_SERVICE_NAME"] = "micro-identity",
            ["ASPNETCORE_ENVIRONMENT"] = "production",
            ["HIS_HOPE_RELEASE_SHA"] = "release-123",
            ["HIS_HOPE_RELEASE_DIGEST"] = "sha256:123",
            ["OTEL_RESOURCE_ATTRIBUTES"] = "service.name=spoofed,deployment.environment.name=spoofed,release.sha=spoofed,tenant.id=evil,openship.project.id=evil,tenant_id=evil,region=ap-southeast-1,description=one%2Ctwo"
        });
        using var provider = services.BuildServiceProvider();
        var attributes = provider.GetRequiredService<TracerProvider>().GetResource().Attributes.ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal("micro-identity", attributes["service.name"]);
        Assert.Equal("production", attributes["deployment.environment.name"]);
        Assert.Equal("release-123", attributes["release.sha"]);
        Assert.Equal("sha256:123", attributes["release.digest"]);
        Assert.Equal("ap-southeast-1", attributes["region"]);
        Assert.Equal("one,two", attributes["description"]);
        Assert.DoesNotContain(attributes.Keys, key => key.Contains("tenant") || key.Contains("project"));
    }

    [Theory]
    [InlineData("DOTNET_ENVIRONMENT", "staging")]
    [InlineData("Environment", "staging")]
    [InlineData("OTEL_RESOURCE_ATTRIBUTES", "deployment.environment.name=staging")]
    public void EnvironmentAliases_AreSupported(string key, string value)
    {
        var settings = TracingConfiguration.Read(Config(new() { [key] = value }), "identity-service");
        Assert.Equal("staging", settings.EnvironmentName);
    }

    [Fact]
    public void UnconfiguredTracing_HasNoOtlpExporterAndPreservesParentSampling()
    {
        var services = Register(new());
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IConfigureOptions<OtlpExporterOptions>));
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource("identity-service");
        using var activity = source.StartActivity("request", ActivityKind.Server, UnsampledParent());

        Assert.False(activity?.Recorded ?? false);
    }

    [Fact]
    public void ConfiguredTracing_RecordsUnsampledRemoteParentsAndUsesW3CPropagation()
    {
        var spans = new CaptureProcessor();
        var services = Register(new() { ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:4317" });
        services.AddOpenTelemetry().WithTracing(tracing => tracing.AddProcessor(spans));
        services.Configure<OtlpExporterOptions>(options => options.HttpClientFactory = () => new HttpClient(new CollectorStub()));
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource("identity-service");
        var parent = UnsampledParent();
        using (var activity = source.StartActivity("request", ActivityKind.Server, parent))
        {
            Assert.NotNull(activity);
            Assert.True(activity.Recorded);
            Assert.True(activity.IsAllDataRequested);
            Assert.Equal(parent.TraceId, activity.TraceId);
            Assert.Equal(parent.SpanId, activity.ParentSpanId);
            Assert.Equal(ActivityIdFormat.W3C, activity.IdFormat);

            var headers = new Dictionary<string, string>();
            Propagators.DefaultTextMapPropagator.Inject(new PropagationContext(activity.Context, default), headers,
                static (carrier, key, value) => carrier[key] = value);
            Assert.Contains(activity.TraceId.ToString(), headers["traceparent"]);
            Assert.DoesNotContain("baggage", headers.Keys);
        }

        Assert.Single(spans.Activities);
        var tracking = provider.GetRequiredService<IOptions<LoggerFactoryOptions>>().Value.ActivityTrackingOptions;
        Assert.True(tracking.HasFlag(ActivityTrackingOptions.TraceId));
        Assert.True(tracking.HasFlag(ActivityTrackingOptions.SpanId));
    }

    [Fact]
    public void ExistingSerilogSpanEnricher_AddsTraceAndSpanIds()
    {
        var sink = new CaptureLogSink();
        using var logger = new LoggerConfiguration().Enrich.WithSpan().WriteTo.Sink(sink).CreateLogger();
        using var activity = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();
        logger.Information("Span log");

        Assert.NotNull(sink.Event);
        Assert.Equal(activity.TraceId.ToString(), ((ScalarValue)sink.Event.Properties["TraceId"]).Value);
        Assert.Equal(activity.SpanId.ToString(), ((ScalarValue)sink.Event.Properties["SpanId"]).Value);
    }

    [Fact]
    public async Task AspNetCoreAndHttpClient_PropagateW3CAndExcludeOnlyProbePaths()
    {
        var spans = new CaptureProcessor();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHisHopeOpenTelemetryExporters(Config(new()
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:4317"
        }), "identity-service");
        builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddProcessor(spans));
        builder.Services.Configure<OtlpExporterOptions>(options =>
            options.HttpClientFactory = () => new HttpClient(new CollectorStub()));
        await using var app = builder.Build();
        foreach (var route in new[] { "/health", "/health/live", "/metrics", "/metrics/prometheus", "/healthcare", "/metrics-report" })
            app.MapGet(route, () => "ok");
        app.MapPost("/echo", () => "ok");
        string? forwardedTraceParent = null;
        ActivityContext? upstream = null;
        ActivityContext? downstream = null;
        app.MapGet("/work", (HttpContext context) =>
        {
            forwardedTraceParent = context.Request.Headers["traceparent"];
            downstream = Activity.Current!.Context;
            return "ok";
        });
        app.MapGet("/forward", async () =>
        {
            upstream = Activity.Current!.Context;
            using var downstreamClient = new HttpClient();
            return await downstreamClient.GetStringAsync(app.Urls.Single() + "/work");
        });
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

        foreach (var probe in new[] { "/health", "/health/live", "/metrics", "/metrics/prometheus" })
            Assert.Equal("ok", await client.GetStringAsync(probe));
        Assert.Empty(spans.Activities);

        Assert.Equal("ok", await client.GetStringAsync("/forward"));
        Assert.NotNull(upstream);
        Assert.NotNull(downstream);
        Assert.Equal(upstream.Value.TraceId, downstream.Value.TraceId);
        Assert.Contains(upstream.Value.TraceId.ToString(), forwardedTraceParent);
        Assert.Contains(spans.Activities, span => span.Kind == ActivityKind.Client && span.TraceId == upstream.Value.TraceId);
        Assert.Equal(2, spans.Activities.Count(span => span.Kind == ActivityKind.Server));

        var count = spans.Activities.Count;
        Assert.Equal("ok", await client.GetStringAsync("/healthcare"));
        Assert.Equal("ok", await client.GetStringAsync("/metrics-report"));
        Assert.Equal(count + 4, spans.Activities.Count);

        client.DefaultRequestHeaders.Authorization = new("Bearer", "tracing-test-auth");
        client.DefaultRequestHeaders.Add("Cookie", "session=tracing-test-cookie");
        using var response = await client.PostAsync("/echo", new StringContent("tracing-test-body"));
        response.EnsureSuccessStatusCode();
        Assert.All(spans.Activities.SelectMany(span => span.TagObjects), tag =>
        {
            Assert.DoesNotContain("authorization", tag.Key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("cookie", tag.Key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("body", tag.Key, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual("db.statement", tag.Key);
            Assert.DoesNotContain("tracing-test-", tag.Value?.ToString() ?? "", StringComparison.Ordinal);
        });
        await app.StopAsync();
    }

    private static ServiceCollection Register(Dictionary<string, string?> values)
    {
        var services = new ServiceCollection();
        services.AddHisHopeOpenTelemetryExporters(Config(values), "identity-service");
        return services;
    }

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ActivityContext UnsampledParent() =>
        new(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None, isRemote: true);

    private sealed class CaptureProcessor : BaseProcessor<Activity>
    {
        public ConcurrentQueue<Activity> Activities { get; } = new();
        public override void OnEnd(Activity activity) => Activities.Enqueue(activity);
    }

    private sealed class CollectorStub : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public Version? RequestVersion { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestVersion = request.Version;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Version = HttpVersion.Version20,
                Content = new ByteArrayContent(new byte[5])
            };
            response.Content.Headers.ContentType = new("application/grpc");
            response.TrailingHeaders.Add("grpc-status", "0");
            return Task.FromResult(response);
        }
    }

    private sealed class CaptureLogSink : ILogEventSink
    {
        public LogEvent? Event { get; private set; }
        public void Emit(LogEvent logEvent) => Event = logEvent;
    }
}
