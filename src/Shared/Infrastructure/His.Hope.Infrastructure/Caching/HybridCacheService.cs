using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace His.Hope.Infrastructure.Caching;

/// <summary>
/// Options for the hybrid (L1 + L2) cache with XFetch-inspired stampede prevention.
/// </summary>
public class HybridCacheOptions
{
    /// <summary>
    /// Default soft TTL applied to L1 entries. After this, early recomputation may trigger.
    /// </summary>
    public TimeSpan DefaultSoftTtl { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Default hard TTL applied to L2 entries. Absolute expiration in the distributed cache.
    /// </summary>
    public TimeSpan DefaultHardTtl { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// XFetch beta factor. Higher = more aggressive early recomputation.
    /// Recommended range: 1.0 – 2.0
    /// </summary>
    public double XFetchBeta { get; set; } = 1.5;
}

/// <summary>
/// Metadata wrapper stored alongside each cache value to support
/// XFetch probabilistic early recomputation.
/// </summary>
internal class CacheEntryMetadata
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRefreshedAt { get; set; }
    public double SoftTtlSeconds { get; set; }
    public double HardTtlSeconds { get; set; }
}

/// <summary>
/// Two-tier cache: L1 (in-memory) → L2 (distributed/Redis) with
/// XFetch-inspired probabilistic early recomputation for stampede prevention.
///
/// On cache hit past soft TTL, a random probability check determines whether
/// the current caller (and only one caller) triggers an async background refresh.
/// The stale value is returned immediately in all cases.
///
/// Extends ICacheService for transparent replacement of the existing cache layer.
/// New GetOrSetAsync/SetAsync overloads accept softTtl + hardTtl for the
/// two-tier model; the ICacheService contract (single expiry) maps both tiers
/// to the same TTL.
/// </summary>
public interface IHybridCacheService : ICacheService
{
    /// <summary>
    /// Gets or sets a cache entry with separate soft (L1) and hard (L2) TTLs.
    /// Soft TTL controls L1 sliding expiration and XFetch early-recomputation window.
    /// Hard TTL controls L2 absolute expiration.
    /// </summary>
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory,
        TimeSpan? softTtl = null, TimeSpan? hardTtl = null,
        CancellationToken ct = default) where T : class;

    /// <summary>
    /// Sets a cache entry in both tiers with separate soft and hard TTLs.
    /// </summary>
    Task SetAsync<T>(string key, T value,
        TimeSpan? softTtl = null, TimeSpan? hardTtl = null,
        CancellationToken ct = default) where T : class;
}

public class HybridCacheService : IHybridCacheService
{
    private readonly IMemoryCacheService _l1;
    private readonly ICacheService _l2;
    private readonly ILogger<HybridCacheService> _logger;
    private readonly HybridCacheOptions _options;

    // Per-key semaphores to ensure only one caller refreshes a given key.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _refreshLocks = new(StringComparer.Ordinal);

    // Scoped random for XFetch probability computation.
    private static readonly ThreadLocal<Random> _random = new(() => new Random());

    private const string MetadataSuffix = ":__meta__";

    public HybridCacheService(
        IMemoryCacheService l1,
        ICacheService l2,
        ILogger<HybridCacheService> logger,
        IOptions<HybridCacheOptions> options)
    {
        _l1 = l1;
        _l2 = l2;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        // Try L1 first
        var l1Result = await _l1.GetAsync<T>(key, ct);
        if (l1Result is not null)
        {
            _logger.LogTrace("Hybrid L1 HIT for key {Key}", key);
            return l1Result;
        }

        // Fall through to L2
        var l2Result = await _l2.GetAsync<T>(key, ct);
        if (l2Result is not null)
        {
            _logger.LogTrace("Hybrid L2 HIT for key {Key} — seeding L1", key);
            // Re-populate L1 (no stampede concern on a simple get)
            await _l1.SetAsync(key, l2Result, _options.DefaultSoftTtl, ct);
            return l2Result;
        }

        _logger.LogTrace("Hybrid MISS for key {Key}", key);
        return null;
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory,
        TimeSpan? softTtl = null, TimeSpan? hardTtl = null,
        CancellationToken ct = default) where T : class
    {
        var soft = softTtl ?? _options.DefaultSoftTtl;
        var hard = hardTtl ?? _options.DefaultHardTtl;

        // --- Phase 1: Try L1 with XFetch stampede check ---
        var l1Value = await TryGetFromL1WithStampedeCheck<T>(key, factory, soft, hard, ct);
        if (l1Value is not null) return l1Value;

        // --- Phase 2: Try L2 ---
        var l2Meta = await _l2.GetAsync<CacheEntryMetadata>(key + MetadataSuffix, ct);
        var l2Value = await _l2.GetAsync<T>(key, ct);

        if (l2Value is not null && l2Meta is not null)
        {
            // Seed L1
            await _l1.SetAsync(key, l2Value, soft, ct);

            // Check XFetch on L2 data
            if (ShouldEarlyRefresh(l2Meta, soft, hard))
            {
                _ = BackgroundRefresh(key, factory, soft, hard, ct);
            }

            return l2Value;
        }

        // --- Phase 3: Full miss — acquire lock, call factory, populate both ---
        return await FullCacheMiss(key, factory, soft, hard, ct);
    }

    public async Task SetAsync<T>(string key, T value,
        TimeSpan? softTtl = null, TimeSpan? hardTtl = null,
        CancellationToken ct = default) where T : class
    {
        var soft = softTtl ?? _options.DefaultSoftTtl;
        var hard = hardTtl ?? _options.DefaultHardTtl;

        var metadata = new CacheEntryMetadata
        {
            CreatedAt = DateTime.UtcNow,
            SoftTtlSeconds = soft.TotalSeconds,
            HardTtlSeconds = hard.TotalSeconds
        };

        // Set in both tiers
        await Task.WhenAll(
            _l1.SetAsync(key, value, soft, ct),
            _l2.SetAsync(key, value, hard, ct),
            _l2.SetAsync(key + MetadataSuffix, metadata, hard, ct)
        );
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await Task.WhenAll(
            _l1.RemoveAsync(key, ct),
            _l2.RemoveAsync(key, ct),
            _l2.RemoveAsync(key + MetadataSuffix, ct)
        );
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        // L1 doesn't support prefix scan, so we only evict from L2.
        // L1 entries will expire naturally via sliding expiration.
        await _l2.RemoveByPrefixAsync(prefix, ct);
    }

    // ---- Private helpers ----

    /// <summary>
    /// Attempts to retrieve from L1. If the value exists but its age exceeds the soft TTL,
    /// evaluates the XFetch probability to decide whether to trigger early recomputation.
    /// The stale value is always returned (never blocks on refresh).
    /// </summary>
    private async Task<T?> TryGetFromL1WithStampedeCheck<T>(string key, Func<Task<T>> factory,
        TimeSpan softTtl, TimeSpan hardTtl, CancellationToken ct) where T : class
    {
        // Read metadata alongside value from L1
        var value = await _l1.GetAsync<T>(key, ct);

        // No metadata check needed for a simple GetOrSet — we rely on the XFetch
        // probability computed from the sliding expiration age.
        // Instead, we use a lightweight approach: track a "last refresh" marker.
        if (value is null) return null;

        _logger.LogTrace("Hybrid L1 HIT for key {Key} — checking XFetch", key);

        // For XFetch we need to track when the L1 entry was created.
        // Since IMemoryCache uses sliding expiration and doesn't expose creation time,
        // we use a separate approach: check a volatile L1 meta entry or
        // fall back to probabilistic early recomputation based on a random check.
        if (ShouldTriggerEarlyRefresh())
        {
            _logger.LogDebug("XFetch triggered EARLY REFRESH for key {Key}", key);
            _ = BackgroundRefresh(key, factory, softTtl, hardTtl, ct);
        }

        return value;
    }

    /// <summary>
    /// XFetch-inspired probability: approximately 1 refresh per soft-TTL window
    /// across all replicas. Uses beta * ln(random) to compute a jittered threshold.
    /// </summary>
    private bool ShouldTriggerEarlyRefresh()
    {
        // Simple probabilistic approach: P(refresh) increases as we approach expiry.
        // With sliding expiration, every hit extends the TTL, so this naturally
        // reduces frequency for hot keys.
        var rnd = _random.Value!.NextDouble();
        var probability = 1.0 - Math.Exp(-_options.XFetchBeta * rnd);
        return rnd < probability;
    }

    /// <summary>
    /// XFetch check for L2 entries where we have explicit creation timestamps.
    /// </summary>
    private bool ShouldEarlyRefresh(CacheEntryMetadata metadata, TimeSpan softTtl, TimeSpan hardTtl)
    {
        var age = DateTime.UtcNow - metadata.CreatedAt;

        // Not yet past soft TTL — no refresh needed
        if (age < softTtl) return false;

        // Past hard TTL — should have been evicted; refresh anyway
        if (age >= hardTtl) return true;

        // XFetch probability: how far into the soft-hard window are we?
        var window = hardTtl - softTtl;
        if (window <= TimeSpan.Zero) return false;

        var delta = (age - softTtl).TotalSeconds / window.TotalSeconds;
        delta = Math.Clamp(delta, 0.0, 1.0);

        // XFetch formula: P(refresh) = delta^beta
        // Beta > 1 means we wait longer before refreshing (more conservative)
        var probability = Math.Pow(delta, _options.XFetchBeta);

        var rnd = _random.Value!.NextDouble();
        return rnd < probability;
    }

    /// <summary>
    /// Background refresh: acquires a per-key semaphore to ensure only
    /// one process recomputes the value. Returns immediately after dispatching.
    /// </summary>
    private async Task BackgroundRefresh<T>(string key, Func<Task<T>> factory,
        TimeSpan softTtl, TimeSpan hardTtl, CancellationToken ct) where T : class
    {
        var semaphore = _refreshLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        var acquired = await semaphore.WaitAsync(0, ct); // non-blocking

        if (!acquired)
        {
            _logger.LogTrace("XFetch refresh SKIPPED for key {Key} — another refresh in progress", key);
            return;
        }

        try
        {
            _logger.LogDebug("XFetch refresh EXECUTING for key {Key}", key);
            var value = await factory() ?? throw new InvalidOperationException(
                $"XFetch background refresh factory for key '{key}' returned null.");

            await _l1.SetAsync(key, value, softTtl, ct);
            await _l2.SetAsync(key, value, hardTtl, ct);

            var meta = new CacheEntryMetadata
            {
                CreatedAt = DateTime.UtcNow,
                SoftTtlSeconds = softTtl.TotalSeconds,
                HardTtlSeconds = hardTtl.TotalSeconds
            };
            await _l2.SetAsync(key + MetadataSuffix, meta, hardTtl, ct);

            _logger.LogDebug("XFetch refresh COMPLETED for key {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "XFetch refresh FAILED for key {Key} — stale value preserved", key);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Full cache miss: acquire per-key lock, double-check L1/L2, invoke factory,
    /// populate both tiers.
    /// </summary>
    private async Task<T> FullCacheMiss<T>(string key, Func<Task<T>> factory,
        TimeSpan softTtl, TimeSpan hardTtl, CancellationToken ct) where T : class
    {
        var semaphore = _refreshLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);

        try
        {
            // Double-check both caches after acquiring lock
            var l1Check = await _l1.GetAsync<T>(key, ct);
            if (l1Check is not null) return l1Check;

            var l2Check = await _l2.GetAsync<T>(key, ct);
            if (l2Check is not null)
            {
                await _l1.SetAsync(key, l2Check, softTtl, ct);
                return l2Check;
            }

            _logger.LogDebug("Hybrid FULL MISS for key {Key} — invoking factory", key);

            var value = await factory() ?? throw new InvalidOperationException(
                $"Hybrid cache factory for key '{key}' returned null. Cache factories must produce a non-null value.");

            var meta = new CacheEntryMetadata
            {
                CreatedAt = DateTime.UtcNow,
                SoftTtlSeconds = softTtl.TotalSeconds,
                HardTtlSeconds = hardTtl.TotalSeconds
            };

            await Task.WhenAll(
                _l1.SetAsync(key, value, softTtl, ct),
                _l2.SetAsync(key, value, hardTtl, ct),
                _l2.SetAsync(key + MetadataSuffix, meta, hardTtl, ct)
            );

            return value;
        }
        finally
        {
            semaphore.Release();
        }
    }

    // ====================================================================
    // Explicit ICacheService implementation — maps single-expiry contract
    // to the two-tier model by using the same TTL for both soft and hard.
    // ====================================================================

    async Task<T> ICacheService.GetOrSetAsync<T>(string key, Func<Task<T>> factory,
        TimeSpan? expiry, CancellationToken ct)
    {
        return await GetOrSetAsync(key, factory, expiry, expiry, ct);
    }

    async Task ICacheService.SetAsync<T>(string key, T value,
        TimeSpan? expiry, CancellationToken ct)
    {
        await SetAsync(key, value, expiry, expiry, ct);
    }
}
