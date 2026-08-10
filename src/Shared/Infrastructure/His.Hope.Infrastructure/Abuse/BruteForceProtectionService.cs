using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Abuse;

/// <summary>
/// Redis-backed brute force protection service.
/// Uses IDistributedCache for fast counters (same pattern as TokenBlacklistService).
/// Each failed attempt increments a Redis counter; after threshold, account is locked.
/// A separate CockroachDB table (login_attempts) provides the permanent audit trail.
///
/// HIPAA Context:
///   164.312(a)(1) Access Control: Account lockout prevents brute-force attacks
///   164.312(d) Person or Entity Authentication: Progressive delay thwarts automated attacks
/// </summary>
public interface IBruteForceProtectionService
{
    /// <summary>Check if account is currently locked due to too many failures.</summary>
    Task<bool> IsAccountLockedAsync(string username, CancellationToken ct = default);

    /// <summary>Record a failed login attempt. Returns the current failure count.</summary>
    Task<int> RecordFailedAttemptAsync(string username, string ip, CancellationToken ct = default);

    /// <summary>Record a successful login and clear all failure counters.</summary>
    Task RecordSuccessAsync(string username, CancellationToken ct = default);
}

public sealed class BruteForceProtectionService : IBruteForceProtectionService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<BruteForceProtectionService> _logger;

    private const string FailCounterPrefix = "HisHope:brute:fail:";
    private const string LockPrefix = "HisHope:brute:lock:";
    private const int MaxFailedAttempts = 10;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan CounterTtl = TimeSpan.FromMinutes(30);

    public BruteForceProtectionService(
        IDistributedCache cache,
        ILogger<BruteForceProtectionService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> IsAccountLockedAsync(string username, CancellationToken ct = default)
    {
        var lockKey = BuildLockKey(username);
        var lockValue = await _cache.GetStringAsync(lockKey, ct);
        return lockValue is not null;
    }

    public async Task<int> RecordFailedAttemptAsync(string username, string ip, CancellationToken ct = default)
    {
        var failKey = BuildFailCounterKey(username);
        var lockKey = BuildLockKey(username);

        // Increment fail counter. IDistributedCache doesn't have INCR,
        // so use a read-increment-write pattern.
        var existing = await _cache.GetStringAsync(failKey, ct);
        var attempts = 1;
        if (existing is not null && int.TryParse(existing, out var parsed))
        {
            attempts = parsed + 1;
        }

        var counterOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CounterTtl
        };
        await _cache.SetStringAsync(failKey, attempts.ToString(), counterOptions, ct);

        _logger.LogWarning(
            "Failed login attempt for {Username} from {Ip} (attempt {Attempts}/{Max})",
            username, ip, attempts, MaxFailedAttempts);

        // Lock account if threshold reached
        if (attempts >= MaxFailedAttempts)
        {
            var lockOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = LockDuration
            };
            await _cache.SetStringAsync(lockKey, "locked", lockOptions, ct);

            _logger.LogCritical(
                "Account LOCKED for {Username} after {Attempts} failed attempts from {Ip}",
                username, attempts, ip);
        }

        return attempts;
    }

    public async Task RecordSuccessAsync(string username, CancellationToken ct = default)
    {
        var failKey = BuildFailCounterKey(username);
        var lockKey = BuildLockKey(username);

        await _cache.RemoveAsync(failKey, ct);
        await _cache.RemoveAsync(lockKey, ct);

        _logger.LogInformation("Successful login for {Username}, cleared brute force counters", username);
    }

    /// <summary>
    /// Get the progressive delay in seconds for a given attempt number.
    /// Implements exponential backoff: 0s, 1s, 2s, 4s, 8s, 15s (capped).
    /// </summary>
    public static int GetProgressiveDelay(int attempts) => attempts switch
    {
        1 => 0,
        2 => 1,
        3 => 2,
        4 => 4,
        5 => 8,
        _ => 15 // 6+ attempts
    };

    private static string BuildFailCounterKey(string username) => FailCounterPrefix + username.ToLowerInvariant();
    private static string BuildLockKey(string username) => LockPrefix + username.ToLowerInvariant();
}
