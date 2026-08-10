using System.Text.Json.Serialization;
using SystemDashboard.Bff.Serialization;

namespace SystemDashboard.Bff.Models;

public sealed record LogEntry
{
    public string? Id { get; init; }
    [JsonConverter(typeof(UtcDateTimeConverter))]
    public DateTime Timestamp { get; init; }
    public required string Level { get; init; }
    public required string Service { get; init; }
    public required string Message { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? Exception { get; init; }
    public Dictionary<string, object>? Properties { get; init; }
}
