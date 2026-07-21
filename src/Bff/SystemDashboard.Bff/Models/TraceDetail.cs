namespace SystemDashboard.Bff.Models;

public sealed record TraceDetail
{
    public required string TraceId { get; init; }
    public List<TraceSpan> Spans { get; init; } = [];
    public Dictionary<string, string> Processes { get; init; } = [];
}

public sealed record TraceSpan
{
    public required string SpanId { get; init; }
    public required string OperationName { get; init; }
    public required string ProcessId { get; init; }
    public long StartTimeUs { get; init; }
    public long DurationUs { get; init; }
    public List<TraceReference> References { get; init; } = [];
    public Dictionary<string, object> Tags { get; init; } = [];
    public List<TraceLog> Logs { get; init; } = [];
}

public sealed record TraceReference
{
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public required string RefType { get; init; }
}

public sealed record TraceLog
{
    public long TimestampUs { get; init; }
    public Dictionary<string, object> Fields { get; init; } = [];
}
