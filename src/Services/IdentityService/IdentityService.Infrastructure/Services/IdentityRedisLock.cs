using StackExchange.Redis;

namespace His.Hope.IdentityService.Infrastructure.Services;

public sealed class IdentityRedisLock(IConnectionMultiplexer redis)
{
    private const string ReleaseScript = "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) else return 0 end";

    public async Task<Lease?> TryAcquireAsync(string key, TimeSpan ttl)
    {
        var token = Guid.NewGuid().ToString("N");
        var acquired = await redis.GetDatabase().StringSetAsync(key, token, ttl, When.NotExists);
        return acquired ? new Lease(redis, key, token) : null;
    }

    public sealed class Lease(IConnectionMultiplexer redis, string key, string token) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await redis.GetDatabase().ScriptEvaluateAsync(
                ReleaseScript,
                new RedisKey[] { key },
                new RedisValue[] { token });
        }
    }
}