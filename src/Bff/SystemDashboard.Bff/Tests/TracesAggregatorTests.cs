using Microsoft.Extensions.Logging;
using NSubstitute;
using SystemDashboard.Bff.Aggregators;
using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;
using Xunit;

namespace SystemDashboard.Bff.Tests;

public sealed class TracesAggregatorTests
{
    [Fact]
    public async Task SearchTracesAsync_PassesParametersThrough()
    {
        // Arrange
        var jaeger = Substitute.For<IJaegerQueryService>();
        var from = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        var expectedTraces = new List<TraceSummary>
        {
            new()
            {
                TraceId = "trace-1",
                RootService = "identity-service",
                RootOperation = "POST /api/login",
                DurationMs = 150,
                SpanCount = 3,
                StartTime = DateTime.UtcNow
            }
        };

        jaeger.SearchTracesAsync(
                Arg.Is<string>(s => s == "identity-service"),
                Arg.Is<DateTime?>(d => d == from),
                Arg.Is<DateTime?>(d => d == to),
                Arg.Is<long?>(d => d == 100),
                Arg.Is<int>(i => i == 50),
                Arg.Any<CancellationToken>())
            .Returns(expectedTraces);

        var logger = Substitute.For<ILogger<TracesAggregator>>();
        var aggregator = new TracesAggregator(jaeger, logger);

        // Act
        var results = await aggregator.SearchTracesAsync(
            service: "identity-service",
            from: from,
            to: to,
            minDurationMs: 100,
            limit: 50);

        // Assert
        Assert.Single(results);
        var trace = results[0];
        Assert.Equal("trace-1", trace.TraceId);
        Assert.Equal("identity-service", trace.RootService);
        Assert.Equal("POST /api/login", trace.RootOperation);
        Assert.Equal(150, trace.DurationMs);
    }

    [Fact]
    public async Task GetTraceAsync_ReturnsTraceDetail()
    {
        // Arrange
        var jaeger = Substitute.For<IJaegerQueryService>();
        var expectedDetail = new TraceDetail
        {
            TraceId = "trace-1",
            Spans =
            [
                new TraceSpan
                {
                    SpanId = "span-1",
                    OperationName = "HTTP GET /api/users",
                    ProcessId = "p1",
                    StartTimeUs = 1000000,
                    DurationUs = 50000
                }
            ],
            Processes = new Dictionary<string, string>
            {
                ["p1"] = "identity-service"
            }
        };

        jaeger.GetTraceAsync("trace-1", default)
            .Returns(expectedDetail);

        var logger = Substitute.For<ILogger<TracesAggregator>>();
        var aggregator = new TracesAggregator(jaeger, logger);

        // Act
        var result = await aggregator.GetTraceAsync("trace-1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("trace-1", result.TraceId);
        Assert.Single(result.Spans);
        Assert.Equal("HTTP GET /api/users", result.Spans[0].OperationName);
        Assert.Equal("identity-service", result.Processes["p1"]);
    }

    [Fact]
    public async Task SearchTracesAsync_ReturnsEmptyOnJaegerFailure()
    {
        // Arrange
        var jaeger = Substitute.For<IJaegerQueryService>();
        jaeger.SearchTracesAsync(
                Arg.Any<string>(),
                Arg.Any<DateTime?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<long?>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns<List<TraceSummary>>(_ => throw new HttpRequestException("Jaeger unavailable"));

        var logger = Substitute.For<ILogger<TracesAggregator>>();
        var aggregator = new TracesAggregator(jaeger, logger);

        // Act
        var results = await aggregator.SearchTracesAsync("identity-service", null, null, null, 20);

        // Assert
        Assert.Empty(results);
    }
}
