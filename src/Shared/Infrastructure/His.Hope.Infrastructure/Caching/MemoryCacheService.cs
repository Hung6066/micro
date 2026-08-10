using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace His.Hope.Infrastructure.Caching;

/// <summary>
/// Options for MemoryCacheService (L1 in-memory cache).
/// </summary>
public class MemoryCacheServiceOptions
{
    /// <summary>
    /// Maximum number of entries before eviction begins. Defaults to 500.
    /// </summary>
    public int SizeLimit { get; set; } = 500;

    /// <summary>
    /// Default sliding expiration. Defaults to 5 minutes.
    /// </summary>
    public TimeSpan DefaultSlidingExpiration { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// L1 in-memory cache wrapping IMemoryCache with size limits and
/// sliding expiration. Used as the fast tier in the hybrid cache.
/// </summary>
public interface IMemoryCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null,
        CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null,
        CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}

public class MemoryCacheService : IMemoryCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheService> _logger;
    private readonly MemoryCacheServiceOptions _options;
    private readonly AuthorizationCacheKeyPartitioner _keyPartitioner;
    private readonly ConcurrentDictionary<string, byte> _knownKeys = new(StringComparer.Ordinal);
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    public MemoryCacheService(
        IMemoryCache cache,
        ILogger<MemoryCacheService> logger,
        IOptions<MemoryCacheServiceOptions> options,
        AuthorizationCacheKeyPartitioner keyPartitioner)
    {
        _cache = cache;
        _logger = logger;
        _options = options.Value;
        _keyPartitioner = keyPartitioner;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        key = _keyPartitioner.Partition(key);
        if (_cache.TryGetValue(key, out T? value))
        {
            _logger.LogTrace("L1 cache HIT for key {Key}", key);
            return Task.FromResult(value);
        }

        _logger.LogTrace("L1 cache MISS for key {Key}", key);
        return Task.FromResult<T?>(null);
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory,
        TimeSpan? expiry = null, CancellationToken ct = default) where T : class
    {
        key = _keyPartitioner.Partition(key);
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            _logger.LogTrace("L1 cache HIT for key {Key}", key);
            return cached;
        }

        _logger.LogTrace("L1 cache MISS for key {Key} — invoking factory", key);
        var value = await factory() ?? throw new InvalidOperationException(
            $"L1 cache factory for key '{key}' returned null. Cache factories must produce a non-null value.");

        var entryOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = expiry ?? _options.DefaultSlidingExpiration,
            Size = 1
        };
        entryOptions.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
        {
            if (evictedKey is string key) _knownKeys.TryRemove(key, out _);
        });
        _cache.Set(key, value, entryOptions);
        _knownKeys[key] = 0;

        return value;
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null,
        CancellationToken ct = default) where T : class
    {
        key = _keyPartitioner.Partition(key);
        var entryOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = expiry ?? _options.DefaultSlidingExpiration,
            Size = 1
        };
        entryOptions.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
        {
            if (evictedKey is string key) _knownKeys.TryRemove(key, out _);
        });
        _cache.Set(key, value, entryOptions);
        _knownKeys[key] = 0;
        _logger.LogTrace("L1 cache SET for key {Key}", key);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        key = _keyPartitioner.Partition(key);
        _cache.Remove(key);
        _knownKeys.TryRemove(key, out _);
        _logger.LogTrace("L1 cache REMOVE for key {Key}", key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        foreach (var key in _knownKeys.Keys.Where(key =>
            key.StartsWith(prefix, StringComparison.Ordinal) ||
            key.Contains($":{prefix}", StringComparison.Ordinal)))
        {
            _cache.Remove(key);
            _knownKeys.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}
