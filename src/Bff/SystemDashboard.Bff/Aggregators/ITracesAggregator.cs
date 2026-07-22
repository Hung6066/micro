using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Aggregators;

public interface ITracesAggregator
{
    Task<List<TraceSummary>> SearchTracesAsync(string service, DateTime? from, DateTime? to, long? minDurationMs, int limit = 20, CancellationToken ct = default);
    Task<TraceDetail?> GetTraceAsync(string traceId, CancellationToken ct = default);
}
