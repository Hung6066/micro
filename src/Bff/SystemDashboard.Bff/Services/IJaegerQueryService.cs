using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Services;

public sealed class JaegerOptions
{
    public const string SectionName = "Jaeger";
    public required string QueryUrl { get; init; }
}

public interface IJaegerQueryService
{
    Task<List<TraceSummary>> SearchTracesAsync(string service, DateTime? from, DateTime? to, long? minDurationMs, int limit = 20, CancellationToken ct = default);
    Task<TraceDetail?> GetTraceAsync(string traceId, CancellationToken ct = default);
}
