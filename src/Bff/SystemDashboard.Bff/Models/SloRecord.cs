using System.Text.Json.Serialization;
using SystemDashboard.Bff.Serialization;

namespace SystemDashboard.Bff.Models;

/// <summary>
/// SLO/SLI record for a single service, aggregated from Prometheus recording rules.
/// </summary>
public sealed record SloRecord
{
    /// <summary>Logical service name (e.g. "identity-service").</summary>
    public required string Service { get; init; }

    /// <summary>Human-readable display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Current availability percentage (0–100).</summary>
    public double Availability { get; init; }

    /// <summary>Error budget remaining percentage (0–100).</summary>
    public double ErrorBudgetRemaining { get; init; }

    /// <summary>Burn rate over the last 1 hour.</summary>
    public double BurnRate1h { get; init; }

    /// <summary>Burn rate over the last 6 hours.</summary>
    public double BurnRate6h { get; init; }

    /// <summary>Current p99 latency in milliseconds.</summary>
    public double LatencyP99 { get; init; }

}

/// <summary>
/// Envelope for the /api/slo response.
/// </summary>
public sealed record SloResponse
{
    public required List<SloRecord> Services { get; init; }

    /// <summary>Aggregate p99 latency sparkline data across all services (last 24h).</summary>
    public List<MetricDataPoint>? SparklineData { get; init; }
}

/// <summary>
/// A single sample from a Prometheus instant vector query, preserving label dimensions.
/// </summary>
public sealed record PrometheusSample
{
    /// <summary>Metric labels (including the "service" label from recording rules).</summary>
    public Dictionary<string, string> Labels { get; init; } = [];

    /// <summary>Sample value.</summary>
    public double Value { get; init; }

    /// <summary>Sample timestamp.</summary>
    [JsonConverter(typeof(UtcDateTimeConverter))]
    public DateTime Timestamp { get; init; }
}
