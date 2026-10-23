using FluentAssertions;
using His.Hope.AspNetCore.Tenancy;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace His.Hope.AspNetCore.Tests;

public sealed class TenantContextEndpointFilterTests
{
    [Fact]
    public async Task Rejects_request_without_a_resolved_context()
    {
        var filter = CreateFilter(new Dictionary<string, string?>());
        var context = CreateHttpContext(new FakeTenantContext(null));

        var result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(context),
            _ => ValueTask.FromResult<object?>(Results.Ok()));

        result.Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Marks_legacy_selector_and_emits_deprecation_headers()
    {
        var filter = CreateFilter(new Dictionary<string, string?>
        {
            ["TenantContext:LegacySelectorEnabled"] = "true",
            ["TenantContext:LegacySelectorSunset"] = "2026-12-31",
        });
        var context = CreateHttpContext(new FakeTenantContext("manufacturing"));
        context.Request.QueryString = new QueryString("?tenantKey=manufacturing");

        var result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(context),
            _ => ValueTask.FromResult<object?>(Results.Ok()));

        result.Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.Headers["Deprecation"].ToString().Should().Be("true");
        context.Response.Headers["Sunset"].ToString().Should().Be("2026-12-31");
        context.Response.Headers["X-HisHope-Tenant-Mode"].ToString().Should().Be("legacy-compatibility");
    }

    [Fact]
    public async Task Records_legacy_selector_telemetry_with_service_path_and_selector_tags()
    {
        long observed = 0;
        string? service = null;
        string? path = null;
        string? selector = null;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "His.Hope.AspNetCore.Tenancy" &&
                instrument.Name == "tenant.legacy_selector.usage")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
        {
            observed += measurement;
            foreach (var tag in tags)
            {
                if (tag.Key == "service") service = tag.Value?.ToString();
                if (tag.Key == "path") path = tag.Value?.ToString();
                if (tag.Key == "selector") selector = tag.Value?.ToString();
            }
        });
        listener.Start();

        var filter = CreateFilter(new Dictionary<string, string?>
        {
            ["ServiceName"] = "manufacturingservice",
        });
        var context = CreateHttpContext(new FakeTenantContext("manufacturing"));
        context.Request.Path = "/api/v1/manufacturing/orders";
        context.Request.QueryString = new QueryString("?tenantKey=manufacturing");

        await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(context),
            _ => ValueTask.FromResult<object?>(Results.Ok()));

        observed.Should().Be(1);
        service.Should().Be("manufacturingservice");
        path.Should().Be("/api/v1/manufacturing/orders");
        selector.Should().Be("query");
    }

    [Fact]
    public async Task Rejects_legacy_selector_when_the_release_flag_is_disabled()
    {
        var filter = CreateFilter(new Dictionary<string, string?>
        {
            ["TenantContext:LegacySelectorEnabled"] = "false",
        });
        var context = CreateHttpContext(new FakeTenantContext("manufacturing"));
        context.Request.QueryString = new QueryString("?tenantKey=manufacturing");

        var result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(context),
            _ => ValueTask.FromResult<object?>(Results.Ok()));

        result.Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status410Gone);
    }

    [Fact]
    public async Task Marks_canonical_context_without_deprecation()
    {
        var filter = CreateFilter(new Dictionary<string, string?>());
        var context = CreateHttpContext(new FakeTenantContext("manufacturing"));

        var result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(context),
            _ => ValueTask.FromResult<object?>(Results.Ok()));

        result.Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.Headers.ContainsKey("Deprecation").Should().BeFalse();
        context.Response.Headers["X-HisHope-Tenant-Mode"].ToString().Should().Be("context");
    }

    private static TenantContextEndpointFilter CreateFilter(IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new TenantContextEndpointFilter(configuration, NullLogger<TenantContextEndpointFilter>.Instance);
    }

    private static DefaultHttpContext CreateHttpContext(IHisHopeTenantContext tenantContext)
    {
        var services = new ServiceCollection()
            .AddSingleton<IHisHopeTenantContext>(tenantContext)
            .BuildServiceProvider();
        return new DefaultHttpContext { RequestServices = services };
    }

    private sealed class FakeTenantContext(string? tenantKey) : IHisHopeTenantContext
    {
        public string? TenantKey { get; } = tenantKey;
        public bool HasTenant => !string.IsNullOrWhiteSpace(TenantKey);
    }

    private sealed class DefaultEndpointFilterInvocationContext(HttpContext httpContext)
        : EndpointFilterInvocationContext
    {
        public override HttpContext HttpContext { get; } = httpContext;
        public override IList<object?> Arguments { get; } = [];
        public override T GetArgument<T>(int index) => (T)Arguments[index]!;
    }
}
