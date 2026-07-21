using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;

namespace SystemDashboard.Bff.Aggregators;

public sealed class MetricsAggregator : IMetricsAggregator
{
    private readonly IPrometheusQueryService _prometheus;
    private readonly ILogger<MetricsAggregator> _logger;

    private static readonly Dictionary<string, string> MetricPromqlTemplates = new()
    {
        ["cpu"] = "rate(process_cpu_seconds_total{service=\"{service}\"}[5m]) * 100",
        ["memory"] = "process_working_set_bytes{service=\"{service}\"} / 1024 / 1024",
        ["requests"] = "rate(http_requests_total{service=\"{service}\"}[5m])",
        ["errors"] = "rate(http_requests_total{service=\"{service}\",status=~\"5..\"}[5m])"
    };

    private static readonly Dictionary<string, (TimeSpan duration, string step)> RangeConfig = new()
    {
        ["5m"]  = (TimeSpan.FromMinutes(5), "15s"),
        ["15m"] = (TimeSpan.FromMinutes(15), "15s"),
        ["1h"]  = (TimeSpan.FromHours(1), "15s"),
        ["6h"]  = (TimeSpan.FromHours(6), "1m"),
        ["24h"] = (TimeSpan.FromHours(24), "1m")
    };

    public MetricsAggregator(IPrometheusQueryService prometheus, ILogger<MetricsAggregator> logger)
    {
        _prometheus = prometheus;
        _logger = logger;
    }

    public async Task<List<MetricSnapshot>> GetMetricsAsync(
        string service, string[] metricNames, string range, CancellationToken ct = default)
    {
        var results = new List<MetricSnapshot>();

        if (!RangeConfig.TryGetValue(range, out var rangeConfig))
        {
            _logger.LogWarning("Invalid range requested: {Range}. Using default 5m.", range);
            rangeConfig = RangeConfig["5m"];
        }

        var end = DateTime.UtcNow;
        var start = end - rangeConfig.duration;

        foreach (var metricName in metricNames)
        {
            if (!MetricPromqlTemplates.TryGetValue(metricName, out var template))
            {
                _logger.LogWarning("Unknown metric name: {MetricName}", metricName);
                continue;
            }

            var promql = template.Replace("{service}", service);

            try
            {
                var dataPoints = await _prometheus.QueryRangeAsync(promql, start, end, rangeConfig.step, ct);
                results.Add(new MetricSnapshot
                {
                    Service = service,
                    MetricName = metricName,
                    DataPoints = dataPoints
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MetricsAggregator failed to query metric {Metric} for {Service}",
                    metricName, service);
                results.Add(new MetricSnapshot
                {
                    Service = service,
                    MetricName = metricName,
                    DataPoints = []
                });
            }
        }

        return results;
    }

    public Task<Dictionary<string, object>> GetSummaryAsync(CancellationToken ct = default)
    {
        // v1: not yet implemented
        return Task.FromResult(new Dictionary<string, object>());
    }
}
