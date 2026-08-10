using Microsoft.Extensions.Caching.Memory;
using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;

namespace SystemDashboard.Bff.Aggregators;

public sealed class LogsAggregator : ILogsAggregator
{
    private readonly IMemoryCache _cache;
    private readonly ILogQueryService _logService;
    private readonly ILogger<LogsAggregator> _logger;

    public LogsAggregator(IMemoryCache cache, ILogQueryService logService, ILogger<LogsAggregator> logger)
    {
        _cache = cache;
        _logService = logService;
        _logger = logger;
    }

    public async Task<List<LogEntry>> QueryLogsAsync(
        string? service = null, string? level = null,
        int? from = null, int size = 100,
        string? searchQuery = null, CancellationToken ct = default)
    {
        var cacheKey = CacheKeys.Logs(service, level, size, searchQuery);
        return await _cache.GetOrCreateAsync(cacheKey, async () =>
        {
            try
            {
                return await _logService.QueryLogsAsync(service, level, from, size, searchQuery, null, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LogsAggregator failed to query logs for {Service}", service);
                return [];
            }
        }, TimeSpan.FromSeconds(5));
    }
}
