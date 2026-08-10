using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Services;

public interface ILogQueryService
{
    Task<List<LogEntry>> QueryLogsAsync(
        string? service = null, string? level = null,
        int? from = null, int size = 100,
        string? searchQuery = null,
        DateTime? afterTimestamp = null,
        CancellationToken ct = default);
}
