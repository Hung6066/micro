using Microsoft.Extensions.Logging;
using NSubstitute;
using SystemDashboard.Bff.Aggregators;
using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;
using Xunit;

namespace SystemDashboard.Bff.Tests;

public sealed class MetricsAggregatorTests
{
    [Fact]
    public async Task GetMetricsAsync_ReturnsMetricsForService()
    {
        // Arrange
        var prometheus = Substitute.For<IPrometheusQueryService>();
        var expectedDataPoints = new List<MetricDataPoint>
        {
            new() { Timestamp = DateTime.UtcNow.AddMinutes(-1), Value = 45.2 },
            new() { Timestamp = DateTime.UtcNow, Value = 48.7 }
        };

        prometheus.QueryRangeAsync(
                Arg.Is<string>(q => q.Contains("process_cpu_seconds_total")),
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Is<string>(s => s == "15s"),
                Arg.Any<CancellationToken>())
            .Returns(expectedDataPoints);

        var logger = Substitute.For<ILogger<MetricsAggregator>>();
        var aggregator = new MetricsAggregator(prometheus, logger);

        // Act
        var results = await aggregator.GetMetricsAsync("identity-service", ["cpu"], "5m");

        // Assert
        Assert.Single(results);
        var snapshot = results[0];
        Assert.Equal("identity-service", snapshot.Service);
        Assert.Equal("cpu", snapshot.MetricName);
        Assert.Equal(2, snapshot.DataPoints.Count);
        Assert.Equal(45.2, snapshot.DataPoints[0].Value);
    }

    [Fact]
    public async Task GetMetricsAsync_ReturnsMultipleMetrics()
    {
        // Arrange
        var prometheus = Substitute.For<IPrometheusQueryService>();

        prometheus.QueryRangeAsync(
                Arg.Is<string>(q => q.Contains("process_cpu_seconds_total")),
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<MetricDataPoint>
            {
                new() { Timestamp = DateTime.UtcNow, Value = 50.0 }
            });

        prometheus.QueryRangeAsync(
                Arg.Is<string>(q => q.Contains("process_working_set_bytes")),
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<MetricDataPoint>
            {
                new() { Timestamp = DateTime.UtcNow, Value = 256.0 }
            });

        var logger = Substitute.For<ILogger<MetricsAggregator>>();
        var aggregator = new MetricsAggregator(prometheus, logger);

        // Act
        var results = await aggregator.GetMetricsAsync("patient-service", ["cpu", "memory"], "5m");

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, m => m.MetricName == "cpu" && m.DataPoints[0].Value == 50.0);
        Assert.Contains(results, m => m.MetricName == "memory" && m.DataPoints[0].Value == 256.0);
    }

    [Fact]
    public async Task GetMetricsAsync_HandlesPrometheusFailure()
    {
        // Arrange
        var prometheus = Substitute.For<IPrometheusQueryService>();
        prometheus.QueryRangeAsync(
                Arg.Any<string>(),
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns<List<MetricDataPoint>>(_ => throw new HttpRequestException("Prometheus unavailable"));

        var logger = Substitute.For<ILogger<MetricsAggregator>>();
        var aggregator = new MetricsAggregator(prometheus, logger);

        // Act
        var results = await aggregator.GetMetricsAsync("identity-service", ["cpu"], "5m");

        // Assert
        Assert.Single(results);
        Assert.Empty(results[0].DataPoints);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsEmptyDictInV1()
    {
        // Arrange
        var prometheus = Substitute.For<IPrometheusQueryService>();
        var logger = Substitute.For<ILogger<MetricsAggregator>>();
        var aggregator = new MetricsAggregator(prometheus, logger);

        // Act
        var summary = await aggregator.GetSummaryAsync();

        // Assert
        Assert.Empty(summary);
    }
}
