namespace SystemDashboard.Bff.Models;

public sealed record MetricSnapshot
{
    public required string Service { get; init; }
    public required string MetricName { get; init; }
    public required List<MetricDataPoint> DataPoints { get; init; }
}

public sealed record MetricDataPoint
{
    public DateTime Timestamp { get; init; }
    public double Value { get; init; }
}
