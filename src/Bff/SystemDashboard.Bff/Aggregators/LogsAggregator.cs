using Microsoft.Extensions.Caching.Memory;
using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;

namespace SystemDashboard.Bff.Aggregators;

public sealed class LogsAggregator : ILogsAggregator
{
    private readonly IMemoryCache _cache;
    private readonly IElasticsearchQueryService _esService;
    private readonly ILogger<LogsAggregator> _logger;

    public LogsAggregator(IMemoryCache cache, IElasticsearchQueryService esService, ILogger<LogsAggregator> logger)
    {
        _cache = cache;
        _esService = esService;
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
                return await _esService.QueryLogsAsync(service, level, from, size, searchQuery, null, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LogsAggregator failed to query logs for {Service}", service);
                return [];
            }
        }, TimeSpan.FromSeconds(5));
    }
}
