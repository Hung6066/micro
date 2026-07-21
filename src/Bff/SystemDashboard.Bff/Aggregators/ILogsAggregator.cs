using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Aggregators;

public interface ILogsAggregator
{
    Task<List<LogEntry>> QueryLogsAsync(
        string? service = null, string? level = null,
        DateTime? from = null, int size = 100,
        string? searchQuery = null, CancellationToken ct = default);
}
