namespace SystemDashboard.Bff.Models;

public sealed record LogEntry
{
    public DateTime Timestamp { get; init; }
    public required string Level { get; init; }
    public required string Service { get; init; }
    public required string Message { get; init; }
    public string? TraceId { get; init; }
    public Dictionary<string, object>? Fields { get; init; }
}
