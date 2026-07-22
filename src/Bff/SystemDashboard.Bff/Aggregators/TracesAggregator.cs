using Microsoft.Extensions.Caching.Memory;
using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;

namespace SystemDashboard.Bff.Aggregators;

public sealed class TracesAggregator : ITracesAggregator
{
    private readonly IJaegerQueryService _jaeger;
    private readonly ILogger<TracesAggregator> _logger;
    private readonly IMemoryCache _cache;

    public TracesAggregator(IJaegerQueryService jaeger, ILogger<TracesAggregator> logger, IMemoryCache cache)
    {
        _jaeger = jaeger;
        _logger = logger;
        _cache = cache;
    }

    public async Task<List<TraceSummary>> SearchTracesAsync(
        string service, DateTime? from, DateTime? to,
        long? minDurationMs, int limit = 20, CancellationToken ct = default)
    {
        var cacheKey = CacheKeys.TraceSearch(service);
        return await _cache.GetOrCreateAsync(cacheKey, async () =>
        {
            try
            {
                return await _jaeger.SearchTracesAsync(service, from, to, minDurationMs, limit, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TracesAggregator failed to search traces for {Service}", service);
                return [];
            }
        }, TimeSpan.FromSeconds(15)) ?? [];
    }

    public async Task<TraceDetail?> GetTraceAsync(string traceId, CancellationToken ct = default)
    {
        var cacheKey = CacheKeys.TraceDetail(traceId);
        return await _cache.GetOrCreateAsync(cacheKey, async () =>
        {
            try
            {
                return await _jaeger.GetTraceAsync(traceId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TracesAggregator failed to get trace {TraceId}", traceId);
                return null;
            }
        }, TimeSpan.FromSeconds(30));
    }
}
