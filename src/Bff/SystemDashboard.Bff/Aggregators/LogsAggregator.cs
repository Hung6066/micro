using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;

namespace SystemDashboard.Bff.Aggregators;

public sealed class LogsAggregator : ILogsAggregator
{
    private readonly IElasticsearchQueryService _esService;
    private readonly ILogger<LogsAggregator> _logger;

    public LogsAggregator(IElasticsearchQueryService esService, ILogger<LogsAggregator> logger)
    {
        _esService = esService;
        _logger = logger;
    }

    public async Task<List<LogEntry>> QueryLogsAsync(
        string? service = null, string? level = null,
        int? from = null, int size = 100,
        string? searchQuery = null, CancellationToken ct = default)
    {
        try
        {
            return await _esService.QueryLogsAsync(service, level, from, size, searchQuery, null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LogsAggregator failed to query logs for {Service}", service);
            return [];
        }
    }
}
