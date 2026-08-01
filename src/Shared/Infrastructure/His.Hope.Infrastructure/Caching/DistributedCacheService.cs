using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
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
    private readonly AuthorizationCacheKeyPartitioner _keyPartitioner;
    private readonly string _instancePrefix;
    private readonly TimeSpan _redisOperationTimeout;
    private readonly TimeSpan _redisCircuitBreakDuration;
    private long _redisUnavailableUntilTicks;

    public DistributedCacheService(
        ILogger<DistributedCacheService> logger,
        IConnectionMultiplexer connectionMultiplexer,
        AuthorizationCacheKeyPartitioner keyPartitioner,
        IConfiguration configuration)
    {
        _logger = logger;
        _connectionMultiplexer = connectionMultiplexer;
        _keyPartitioner = keyPartitioner;
        _instancePrefix = "HisHope:";
        _redisOperationTimeout = TimeSpan.FromMilliseconds(Math.Clamp(
            configuration.GetValue("Caching:RedisOperationTimeoutMilliseconds", 500), 100, 5000));
        _redisCircuitBreakDuration = TimeSpan.FromSeconds(Math.Clamp(
            configuration.GetValue("Caching:RedisCircuitBreakSeconds", 15), 1, 300));
    }

    private IDatabase GetDatabase() => _connectionMultiplexer.GetDatabase();

    private bool IsRedisAvailable()
    {
        if (DateTime.UtcNow.Ticks < Interlocked.Read(ref _redisUnavailableUntilTicks))
            return false;

        if (_connectionMultiplexer.IsConnected) return true;

        _logger.LogWarning("Redis cache is unavailable; bypassing distributed cache operation.");
        return false;
    }

    private void MarkRedisUnavailable(Exception exception)
    {
        var until = DateTime.UtcNow.Add(_redisCircuitBreakDuration).Ticks;
        Interlocked.Exchange(ref _redisUnavailableUntilTicks, until);
        _logger.LogWarning(exception,
            "Redis cache operation failed; bypassing distributed cache for {DurationSeconds} seconds.",
            _redisCircuitBreakDuration.TotalSeconds);
    }

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
        if (!IsRedisAvailable()) return null;

        try
        {
            var redisKey = (RedisKey)(_instancePrefix + _keyPartitioner.Partition(key));
            var cached = await WithTimeout(GetDatabase().StringGetAsync(redisKey), _redisOperationTimeout);
            if (cached.IsNullOrEmpty) return null;
            return JsonConvert.DeserializeObject<T>(cached.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache GET failed for key {Key}", key);
            MarkRedisUnavailable(ex);
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
        if (!IsRedisAvailable()) return;

        try
        {
            var redisKey = (RedisKey)(_instancePrefix + _keyPartitioner.Partition(key));
            var serialized = JsonConvert.SerializeObject(value);
            if (expiry.HasValue)
                await WithTimeout(GetDatabase().StringSetAsync(redisKey, serialized, expiry), _redisOperationTimeout);
            else
                await WithTimeout(GetDatabase().StringSetAsync(redisKey, serialized), _redisOperationTimeout);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache SET failed for key {Key}", key);
            MarkRedisUnavailable(ex);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        if (!IsRedisAvailable()) return;

        try
        {
            var redisKey = (RedisKey)(_instancePrefix + _keyPartitioner.Partition(key));
            await WithTimeout(GetDatabase().KeyDeleteAsync(redisKey), _redisOperationTimeout);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REMOVE failed for key {Key}", key);
            MarkRedisUnavailable(ex);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        if (!IsRedisAvailable()) return;

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
                await WithTimeout(GetDatabase().KeyDeleteAsync(keysToDelete.ToArray()), _redisOperationTimeout);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REMOVE by prefix failed for prefix {Prefix}", prefix);
            MarkRedisUnavailable(ex);
        }
    }
}
