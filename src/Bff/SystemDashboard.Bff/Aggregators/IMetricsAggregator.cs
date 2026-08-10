using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Aggregators;

public interface IMetricsAggregator
{
    Task<List<MetricSnapshot>> GetMetricsAsync(string service, string[] metricNames, string range, CancellationToken ct = default);
    Task<Dictionary<string, object>> GetSummaryAsync(CancellationToken ct = default);
}
