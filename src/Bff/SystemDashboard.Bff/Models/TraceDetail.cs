namespace SystemDashboard.Bff.Models;

public sealed record TraceDetail
{
    public required string TraceId { get; init; }
    public required string RootService { get; init; }
    public required string RootName { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public long DurationMs { get; init; }
    public int SpanCount { get; init; }
    public string Status { get; init; } = "Ok";
    public List<TraceSpanEx> Spans { get; init; } = [];
    public List<string> Services { get; init; } = [];
}

public sealed record TraceSpanEx
{
    public required string SpanId { get; init; }
    public string? ParentSpanId { get; init; }
    public required string Name { get; init; }
    public required string Service { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public double DurationMs { get; init; }
    public string Status { get; init; } = "Ok";
    public Dictionary<string, string>? Attributes { get; init; }
    public List<TraceSpanEvent>? Events { get; init; }
}

public sealed record TraceSpanEvent
{
    public required string Name { get; init; }
    public DateTime Timestamp { get; init; }
    public Dictionary<string, string>? Attributes { get; init; }
}
