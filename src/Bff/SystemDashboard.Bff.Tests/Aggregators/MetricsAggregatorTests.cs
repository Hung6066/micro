using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SystemDashboard.Bff.Aggregators;
using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;

namespace SystemDashboard.Bff.Tests.Aggregators;

public sealed class MetricsAggregatorTests
{
    [Fact]
    public async Task GetMetricsAsync_ReturnsEmptySnapshot_OnPrometheusFailure()
    {
        var prometheus = new Mock<IPrometheusQueryService>();
        prometheus.Setup(p => p.QueryRangeAsync(It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Simulated failure"));

        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = NullLogger<MetricsAggregator>.Instance;

        var aggregator = new MetricsAggregator(prometheus.Object, logger, cache);

        var results = await aggregator.GetMetricsAsync(
            "identity-service", ["cpu", "memory"], "5m");

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(0, r.CurrentValue));
        Assert.All(results, r => Assert.Empty(r.DataPoints ?? []));
    }

    [Fact]
    public async Task GetMetricsAsync_QueriesAllMetricsInParallel()
    {
        var prometheus = new Mock<IPrometheusQueryService>();
        prometheus.Setup(p => p.QueryRangeAsync(It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(100);
                return new List<MetricDataPoint>
                {
                    new() { Timestamp = DateTime.UtcNow, Value = 50.0 }
                };
            });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = NullLogger<MetricsAggregator>.Instance;

        var aggregator = new MetricsAggregator(prometheus.Object, logger, cache);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = await aggregator.GetMetricsAsync(
            "identity-service", ["cpu", "memory", "requests", "errors"], "5m");
        sw.Stop();

        Assert.Equal(4, results.Count);
        Assert.True(sw.ElapsedMilliseconds < 300, $"Expected <300ms parallel, got {sw.ElapsedMilliseconds}ms");
    }
}
