using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace His.Hope.Infrastructure.Locking;

/// <summary>
/// Redis-backed distributed lock manager implementing a RedLock-style algorithm.
/// Uses atomic SET NX for acquisition and Lua scripting for safe release/extend.
/// Generates monotonically increasing fencing tokens per process.
/// </summary>
public sealed class RedisLockManager : ILockManager
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<RedisLockManager> _logger;
    private static long _fencingTokenCounter;

    private const string LockKeyPrefix = "hishop:lock:";
    private const int DefaultTtlSeconds = 30;
    private const int MaxTtlSeconds = 300; // 5 minutes safety cap

    // Lua: atomically delete the key only if the token matches
    private const string ReleaseScript =
        "if redis.call('GET', KEYS[1]) == ARGV[1] then " +
        "return redis.call('DEL', KEYS[1]) " +
        "else return 0 end";

    // Lua: atomically extend the key TTL only if the token matches
    private const string ExtendScript =
        "if redis.call('GET', KEYS[1]) == ARGV[1] then " +
        "return redis.call('PEXPIRE', KEYS[1], ARGV[2]) " +
        "else return 0 end";

    public RedisLockManager(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisLockManager> logger)
    {
        _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IDistributedLock?> AcquireAsync(string key, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var lockKey = BuildLockKey(key);
        var fencingToken = Interlocked.Increment(ref _fencingTokenCounter);
        var expiry = SanitizeTtl(ttl ?? TimeSpan.FromSeconds(DefaultTtlSeconds));

        var db = _connectionMultiplexer.GetDatabase();

        try
        {
            var acquired = await db.StringSetAsync(
                lockKey,
                fencingToken.ToString(),
                expiry,
                When.NotExists,
                CommandFlags.DemandMaster);

            if (!acquired)
            {
                _logger.LogDebug("Failed to acquire lock for key {LockKey}", lockKey);
                return null;
            }

            _logger.LogDebug("Acquired lock for key {LockKey} with fencing token {Token}", lockKey, fencingToken);
            return new RedisDistributedLock(this, lockKey, fencingToken);
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex, "Redis error while acquiring lock for key {LockKey}", lockKey);
            return null;
        }
        catch (OperationCanceledException)
        {
            // If cancellation happened mid-flight, attempt to clean up a partial acquisition
            await TryCleanup(lockKey, fencingToken);
            throw;
        }
    }

    internal async Task<bool> ReleaseAsync(string lockKey, long fencingToken)
    {
        var db = _connectionMultiplexer.GetDatabase();

        try
        {
            var result = await db.ScriptEvaluateAsync(
                ReleaseScript,
                new RedisKey[] { lockKey },
                new RedisValue[] { fencingToken.ToString() });

            var released = (long)result == 1;
            if (released)
            {
                _logger.LogDebug("Released lock for key {LockKey} with token {Token}", lockKey, fencingToken);
            }
            else
            {
                _logger.LogWarning(
                    "Attempted to release lock for key {LockKey} with stale token {Token}; lock may be held by another owner",
                    lockKey, fencingToken);
            }

            return released;
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex, "Redis error while releasing lock for key {LockKey}", lockKey);
            return false;
        }
    }

    internal async Task<bool> ExtendAsync(string lockKey, long fencingToken, TimeSpan ttl)
    {
        var db = _connectionMultiplexer.GetDatabase();
        var expiryMs = (long)SanitizeTtl(ttl).TotalMilliseconds;

        try
        {
            var result = await db.ScriptEvaluateAsync(
                ExtendScript,
                new RedisKey[] { lockKey },
                new RedisValue[] { fencingToken.ToString(), expiryMs.ToString() });

            var extended = (long)result == 1;
            if (extended)
            {
                _logger.LogDebug("Extended lock for key {LockKey} with token {Token}", lockKey, fencingToken);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to extend lock for key {LockKey} with token {Token}; lock may have been lost",
                    lockKey, fencingToken);
            }

            return extended;
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex, "Redis error while extending lock for key {LockKey}", lockKey);
            return false;
        }
    }

    private static string BuildLockKey(string key)
    {
        // Sanitize key to prevent Redis key collisions
        var sanitized = key.Replace(' ', '-');
        return $"{LockKeyPrefix}{sanitized}";
    }

    private static TimeSpan SanitizeTtl(TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
            return TimeSpan.FromSeconds(DefaultTtlSeconds);

        if (ttl > TimeSpan.FromSeconds(MaxTtlSeconds))
            return TimeSpan.FromSeconds(MaxTtlSeconds);

        return ttl;
    }

    private async Task TryCleanup(string lockKey, long fencingToken)
    {
        try
        {
            await ReleaseAsync(lockKey, fencingToken);
        }
        catch
        {
            // Best-effort cleanup during cancellation
        }
    }

    /// <summary>
    /// Internal lock handle returned to callers. Tracks fencing token and enables
    /// safe release via both explicit <see cref="ReleaseAsync"/> and implicit disposal.
    /// </summary>
    private sealed class RedisDistributedLock : IDistributedLock
    {
        private readonly RedisLockManager _manager;
        private int _disposed;

        public string Key { get; }
        public long FencingToken { get; }

        internal RedisDistributedLock(RedisLockManager manager, string key, long fencingToken)
        {
            _manager = manager;
            Key = key;
            FencingToken = fencingToken;
        }

        public async Task ReleaseAsync(CancellationToken ct = default)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            await _manager.ReleaseAsync(Key, FencingToken).ConfigureAwait(false);
        }

        public async Task<bool> ExtendAsync(TimeSpan ttl, CancellationToken ct = default)
        {
            ObjectDisposedException.ThrowIf(_disposed == 1, this);
            return await _manager.ExtendAsync(Key, FencingToken, ttl).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            await _manager.ReleaseAsync(Key, FencingToken).ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }
}
