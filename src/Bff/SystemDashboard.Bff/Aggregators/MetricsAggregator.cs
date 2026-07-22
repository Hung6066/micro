using Microsoft.Extensions.Caching.Memory;
using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;

namespace SystemDashboard.Bff.Aggregators;

public sealed class MetricsAggregator : IMetricsAggregator
{
    private readonly IPrometheusQueryService _prometheus;
    private readonly ILogger<MetricsAggregator> _logger;
    private readonly IMemoryCache _cache;

    private static readonly Dictionary<string, string> MetricPromqlTemplates = new()
    {
        ["cpu"] = "rate(process_cpu_time_seconds_total{job=\"{job}\"}[5m]) * 100",
        ["memory"] = "process_memory_usage_bytes{job=\"{job}\"} / 1024 / 1024",
        ["requests"] = "rate(http_server_request_duration_seconds_count{job=\"{job}\"}[5m])",
        ["errors"] = "rate(http_server_request_duration_seconds_count{job=\"{job}\",http_response_status_code=~\"5..\"}[5m])"
    };

    private static readonly Dictionary<string, (string DisplayName, string Unit)> MetricConfig = new()
    {
        ["cpu"] = ("CPU", "%"),
        ["memory"] = ("Memory", "MB"),
        ["requests"] = ("Requests", "req/s"),
        ["errors"] = ("Errors", "errors/min"),
    };

    /// <summary>
    /// Maps kebab-case service names (e.g. "identity-service") to Prometheus job labels (e.g. "identityservice").
    /// </summary>
    private static readonly Dictionary<string, string> ServiceToJobMap = new()
    {
        ["identity-service"] = "identityservice",
        ["patient-service"] = "patientservice",
        ["appointment-service"] = "appointmentservice",
        ["clinical-service"] = "clinicalservice",
        ["lab-service"] = "labservice",
        ["billing-service"] = "billingservice",
        ["pharmacy-service"] = "pharmacyservice",
    };

    private static readonly Dictionary<string, (TimeSpan duration, string step)> RangeConfig = new()
    {
        ["5m"]  = (TimeSpan.FromMinutes(5), "15s"),
        ["15m"] = (TimeSpan.FromMinutes(15), "15s"),
        ["1h"]  = (TimeSpan.FromHours(1), "15s"),
        ["6h"]  = (TimeSpan.FromHours(6), "1m"),
        ["24h"] = (TimeSpan.FromHours(24), "1m")
    };

    public MetricsAggregator(
        IPrometheusQueryService prometheus,
        ILogger<MetricsAggregator> logger,
        IMemoryCache cache)
    {
        _prometheus = prometheus;
        _logger = logger;
        _cache = cache;
    }

    private static TimeSpan GetMetricsTtl(string range) => range switch
    {
        "5m" => TimeSpan.FromSeconds(10),
        "15m" => TimeSpan.FromSeconds(15),
        "1h" => TimeSpan.FromSeconds(30),
        "6h" => TimeSpan.FromSeconds(60),
        "24h" => TimeSpan.FromSeconds(120),
        _ => TimeSpan.FromSeconds(10)
    };

    public async Task<List<MetricSnapshot>> GetMetricsAsync(
        string service, string[] metricNames, string range, CancellationToken ct = default)
    {
        var metricsKey = string.Join(",", metricNames.OrderBy(m => m));
        var cacheKey = CacheKeys.Metrics(service, metricsKey, range);

        return await _cache.GetOrCreateAsync(cacheKey, async () =>
        {
            if (!RangeConfig.TryGetValue(range, out var rangeConfig))
            {
                _logger.LogWarning("Invalid range requested: {Range}. Using default 5m.", range);
                rangeConfig = RangeConfig["5m"];
            }

            var end = DateTime.UtcNow;
            var start = end - rangeConfig.duration;

            var tasks = metricNames.Select(async metricName =>
            {
                if (!MetricPromqlTemplates.TryGetValue(metricName, out var template))
                {
                    _logger.LogWarning("Unknown metric name: {MetricName}", metricName);
                    return null;
                }

                var (displayName, unit) = MetricConfig.TryGetValue(metricName, out var cfg)
                    ? cfg
                    : (metricName, "");

                var job = ServiceToJobMap.TryGetValue(service, out var mappedJob) ? mappedJob : service;
                var promql = template.Replace("{job}", job);

                try
                {
                    var dataPoints = await _prometheus.QueryRangeAsync(promql, start, end, rangeConfig.step, ct);
                    var values = dataPoints.Select(dp => dp.Value).ToList();
                    return new MetricSnapshot
                    {
                        Name = metricName,
                        DisplayName = displayName,
                        Unit = unit,
                        CurrentValue = values.Count > 0 ? values[^1] : 0.0,
                        PreviousValue = values.Count > 1 ? (double?)values[^2] : null,
                        Min = values.Count > 0 ? (double?)values.Min() : null,
                        Max = values.Count > 0 ? (double?)values.Max() : null,
                        Avg = values.Count > 0 ? (double?)values.Average() : null,
                        DataPoints = dataPoints
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Metrics query failed: {Metric} for {Service}", metricName, service);
                    return MetricSnapshot.Empty(metricName, displayName, unit);
                }
            });

            var results = await Task.WhenAll(tasks);
            return results.Where(r => r is not null).Cast<MetricSnapshot>().ToList();
        }, GetMetricsTtl(range));
    }

    public Task<Dictionary<string, object>> GetSummaryAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new Dictionary<string, object>());
    }
}
