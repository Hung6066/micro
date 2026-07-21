using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Services;

public sealed class PrometheusOptions
{
    public const string SectionName = "Prometheus";
    public required string Url { get; init; }
}

public interface IPrometheusQueryService
{
    Task<List<MetricDataPoint>> QueryRangeAsync(string query, DateTime start, DateTime end, string step, CancellationToken ct = default);
}
