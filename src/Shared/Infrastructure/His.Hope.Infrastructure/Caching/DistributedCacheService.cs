using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace His.Hope.Infrastructure.Caching;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null,
        CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null,
        CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}

public class DistributedCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedCacheService> _logger;
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly string _instancePrefix;
    private readonly DistributedCacheEntryOptions _defaultOptions;

    public DistributedCacheService(
        IDistributedCache cache,
        ILogger<DistributedCacheService> logger,
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<RedisCacheOptions> redisCacheOptions)
    {
        _cache = cache;
        _logger = logger;
        _connectionMultiplexer = connectionMultiplexer;
        _instancePrefix = redisCacheOptions.Value.InstanceName ?? string.Empty;
        _defaultOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
            SlidingExpiration = TimeSpan.FromMinutes(2),
        };
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        where T : class
    {
        try
        {
            var cached = await _cache.GetStringAsync(key, ct);
            return cached is null ? null : JsonConvert.DeserializeObject<T>(cached);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache GET failed for key {Key}", key);
            return null;
        }
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory,
        TimeSpan? expiry = null, CancellationToken ct = default)
        where T : class
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null) return cached;

        var value = await factory();
        await SetAsync(key, value, expiry, ct);
        return value;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null,
        CancellationToken ct = default) where T : class
    {
        try
        {
            var options = expiry.HasValue
                ? new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiry
                }
                : _defaultOptions;

            var serialized = JsonConvert.SerializeObject(value);
            await _cache.SetStringAsync(key, serialized, options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache SET failed for key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _cache.RemoveAsync(key, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REMOVE failed for key {Key}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        try
        {
            var endpoints = _connectionMultiplexer.GetEndPoints();
            var keysToDelete = new List<RedisKey>();

            foreach (var endpoint in endpoints)
            {
                var server = _connectionMultiplexer.GetServer(endpoint);

                if (!server.IsConnected) continue;

                var pattern = $"{_instancePrefix}{prefix}*";

                await foreach (var key in server.KeysAsync(pattern: pattern))
                {
                    keysToDelete.Add(key);
                }
            }

            if (keysToDelete.Count > 0)
            {
                var db = _connectionMultiplexer.GetDatabase();
                await db.KeyDeleteAsync(keysToDelete.ToArray(), flags: CommandFlags.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REMOVE by prefix failed for prefix {Prefix}", prefix);
        }
    }
}
