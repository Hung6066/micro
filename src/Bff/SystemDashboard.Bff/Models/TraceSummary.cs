namespace SystemDashboard.Bff.Models;

public sealed record TraceSummary
{
    public required string TraceId { get; init; }
    public required string RootService { get; init; }
    public required string RootName { get; init; }
    public long DurationMs { get; init; }
    public int SpanCount { get; init; }
    public DateTime StartTime { get; init; }
    public string Status { get; init; } = "Ok";
    public bool HasErrors { get; init; }
}
