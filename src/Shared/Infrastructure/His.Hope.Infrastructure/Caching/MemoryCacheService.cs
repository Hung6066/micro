using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
}

public class MemoryCacheService : IMemoryCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheService> _logger;
    private readonly MemoryCacheServiceOptions _options;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    public MemoryCacheService(
        IMemoryCache cache,
        ILogger<MemoryCacheService> logger,
        IOptions<MemoryCacheServiceOptions> options)
    {
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
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
        _cache.Set(key, value, entryOptions);

        return value;
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null,
        CancellationToken ct = default) where T : class
    {
        var entryOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = expiry ?? _options.DefaultSlidingExpiration,
            Size = 1
        };
        _cache.Set(key, value, entryOptions);
        _logger.LogTrace("L1 cache SET for key {Key}", key);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
        _logger.LogTrace("L1 cache REMOVE for key {Key}", key);
        return Task.CompletedTask;
    }
}
