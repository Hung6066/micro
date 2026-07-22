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

    Task<MetricDataPoint?> QueryAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Executes an instant Prometheus query and returns ALL result series
    /// (unlike QueryAsync which returns only the first). Each result includes
    /// its label dimensions and value.
    /// </summary>
    Task<List<PrometheusSample>> QuerySamplesAsync(string query, CancellationToken ct = default);
}
