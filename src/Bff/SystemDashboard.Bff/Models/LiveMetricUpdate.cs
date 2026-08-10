namespace SystemDashboard.Bff.Models;

/// <summary>
/// Real-time metric snapshot pushed via SignalR every 2 seconds per service.
/// </summary>
public sealed record LiveMetricUpdate
{
    public required string ServiceName { get; init; }
    public double Cpu { get; init; }
    public double Memory { get; init; }
    public double Requests { get; init; }
    public DateTime Timestamp { get; init; }
}
