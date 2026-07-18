using System.Diagnostics;
using System.Diagnostics.Metrics;
using His.Hope.Infrastructure.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Degradation;

/// <summary>
/// Fallback policy that serves stale cached data when downstream operations fail.
/// Implements <see cref="IDegradedResponseProvider"/> by maintaining a secondary
/// cache layer with extended TTL for graceful degradation scenarios.
///
/// On DB/Redis failure → serves the last successfully cached value ignoring normal TTL.
/// Adds X-Degraded-Data: true response header when serving stale data.
/// Exposes a degradation counter as a Prometheus metric via OpenTelemetry.
/// </summary>
public sealed class StaleCacheFallbackPolicy : IDegradedResponseProvider, IDisposable
{
    private readonly ICacheService _cache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<StaleCacheFallbackPolicy> _logger;

    // Metrics
    private static readonly Meter DegradationMeter = new(
        "His.Hope.Infrastructure.Degradation",
        "1.0.0");
    private readonly Counter<int> _degradationCounter = DegradationMeter.CreateCounter<int>(
        name: "degraded_response_served_total",
        description: "Total number of degraded (stale cache) responses served.");
    private readonly Histogram<double> _degradationDuration = DegradationMeter.CreateHistogram<double>(
        name: "degraded_response_duration_seconds",
        description: "Duration of degraded response fallback in seconds.");

    // Stale data is stored with a 24-hour TTL under a distinct key prefix
    private static readonly TimeSpan StaleTtl = TimeSpan.FromHours(24);
    private const string StaleKeyPrefix = "stale:";

    public StaleCacheFallbackPolicy(
        ICacheService cache,
        IHttpContextAccessor httpContextAccessor,
        ILogger<StaleCacheFallbackPolicy> logger)
    {
        _cache = cache;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<T?> GetDegradedResponseAsync<T>(string cacheKey, CancellationToken ct = default)
        where T : class
    {
        var staleKey = BuildStaleKey(cacheKey);
        var startTime = Stopwatch.GetTimestamp();

        try
        {
            var staleValue = await _cache.GetAsync<T>(staleKey, ct);

            if (staleValue is not null)
            {
                var elapsed = Stopwatch.GetElapsedTime(startTime);
                _degradationCounter.Add(1);
                _degradationDuration.Record(elapsed.TotalSeconds);

                // Set X-Degraded-Data header on the current HTTP response (if present)
                SetDegradedResponseHeader();

                _logger.LogWarning(
                    "Serving stale degraded response for key {CacheKey} (elapsed: {ElapsedMs:F1}ms)",
                    cacheKey, elapsed.TotalMilliseconds);
            }
            else
            {
                _logger.LogDebug(
                    "No stale degraded response available for key {CacheKey}",
                    cacheKey);
            }

            return staleValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve stale degraded response for key {CacheKey}",
                cacheKey);
            return null;
        }
    }

    /// <inheritdoc />
    public bool HasDegradedResponse(string cacheKey)
    {
        // In-memory check is not available via ICacheService (async only).
        // We optimistically attempt GetDegradedResponseAsync and treat null as "not available".
        // Callers should prefer the async overload when possible.
        return false;
    }

    /// <inheritdoc />
    public async Task RecordSuccessfulResponseAsync<T>(
        string cacheKey, T value, CancellationToken ct = default) where T : class
    {
        var staleKey = BuildStaleKey(cacheKey);

        try
        {
            await _cache.SetAsync(staleKey, value, StaleTtl, ct);
            _logger.LogTrace(
                "Recorded stale backup for key {CacheKey} with {TtlHours}h TTL",
                cacheKey, StaleTtl.TotalHours);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to record stale backup for key {CacheKey}",
                cacheKey);
        }
    }

    private static string BuildStaleKey(string cacheKey) =>
        string.Concat(StaleKeyPrefix, cacheKey);

    private void SetDegradedResponseHeader()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Response is null) return;

        // Avoid overwriting if already set by a previous fallback in the same request
        if (!httpContext.Response.Headers.ContainsKey("X-Degraded-Data"))
        {
            httpContext.Response.Headers["X-Degraded-Data"] = "true";
        }
    }

    public void Dispose()
    {
        DegradationMeter.Dispose();
    }
}
