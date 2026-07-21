using Microsoft.Extensions.Logging;
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
    private readonly ILogger<DistributedCacheService> _logger;
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly string _instancePrefix;
    private static readonly TimeSpan RedisOpTimeout = TimeSpan.FromSeconds(5);

    public DistributedCacheService(
        ILogger<DistributedCacheService> logger,
        IConnectionMultiplexer connectionMultiplexer)
    {
        _logger = logger;
        _connectionMultiplexer = connectionMultiplexer;
        _instancePrefix = "HisHope:";
    }

    private IDatabase GetDatabase() => _connectionMultiplexer.GetDatabase();

    private static async Task<T> WithTimeout<T>(Task<T> task, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token));
        if (completed == task)
            return await task;
        throw new TimeoutException("Redis operation timed out");
    }

    private static async Task WithTimeout(Task task, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token));
        if (completed == task)
            await task;
        else
            throw new TimeoutException("Redis operation timed out");
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        where T : class
    {
        try
        {
            var redisKey = (RedisKey)(_instancePrefix + key);
            var cached = await WithTimeout(GetDatabase().StringGetAsync(redisKey), RedisOpTimeout);
            if (cached.IsNullOrEmpty) return null;
            return JsonConvert.DeserializeObject<T>(cached.ToString());
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
            var redisKey = (RedisKey)(_instancePrefix + key);
            var serialized = JsonConvert.SerializeObject(value);
            if (expiry.HasValue)
                await WithTimeout(GetDatabase().StringSetAsync(redisKey, serialized, expiry), RedisOpTimeout);
            else
                await WithTimeout(GetDatabase().StringSetAsync(redisKey, serialized), RedisOpTimeout);
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
            var redisKey = (RedisKey)(_instancePrefix + key);
            await WithTimeout(GetDatabase().KeyDeleteAsync(redisKey), RedisOpTimeout);
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
                await WithTimeout(GetDatabase().KeyDeleteAsync(keysToDelete.ToArray()), RedisOpTimeout);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REMOVE by prefix failed for prefix {Prefix}", prefix);
        }
    }
}
