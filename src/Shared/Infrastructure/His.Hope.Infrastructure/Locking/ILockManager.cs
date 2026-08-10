namespace His.Hope.Infrastructure.Locking;

/// <summary>
/// Manages distributed lock acquisition backed by Redis (RedLock-style algorithm).
/// Provides monotonically increasing fencing tokens for safe concurrent access.
/// </summary>
public interface ILockManager
{
    /// <summary>
    /// Attempts to acquire a distributed lock for the specified key.
    /// Returns null if the lock is held by another consumer.
    /// </summary>
    /// <param name="key">The resource key to lock on.</param>
    /// <param name="ttl">Time-to-live for the lock. Defaults to 30 seconds if not specified.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="IDistributedLock"/> if acquired, null otherwise.</returns>
    Task<IDistributedLock?> AcquireAsync(string key, TimeSpan? ttl = null, CancellationToken ct = default);
}

/// <summary>
/// Represents an acquired distributed lock that must be released via <see cref="ReleaseAsync"/>
/// or by disposing (via <see cref="IAsyncDisposable"/>).
/// </summary>
public interface IDistributedLock : IAsyncDisposable
{
    /// <summary>The resource key this lock was acquired on.</summary>
    string Key { get; }

    /// <summary>
    /// Monotonically increasing fencing token that proves this client holds the lock.
    /// Use this token to guard writes to external resources (e.g., include in write operations
    /// to detect stale locks).
    /// </summary>
    long FencingToken { get; }

    /// <summary>Releases the distributed lock explicitly.</summary>
    Task ReleaseAsync(CancellationToken ct = default);

    /// <summary>
    /// Extends the lock TTL by the specified duration.
    /// Returns true if the extension succeeded (lock still held), false if the lock was lost.
    /// </summary>
    Task<bool> ExtendAsync(TimeSpan ttl, CancellationToken ct = default);
}
