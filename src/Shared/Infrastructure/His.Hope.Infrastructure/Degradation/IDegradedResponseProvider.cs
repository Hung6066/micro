namespace His.Hope.Infrastructure.Degradation;

/// <summary>
/// Provides stale cached data when downstream systems fail, enabling
/// graceful degradation instead of hard failures.
/// </summary>
public interface IDegradedResponseProvider
{
    /// <summary>
    /// Retrieves a previously stored degraded (stale) response for the given
    /// cache key, ignoring normal TTL expiration.
    /// Returns null when no stale data is available.
    /// </summary>
    Task<T?> GetDegradedResponseAsync<T>(string cacheKey, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Checks whether a degraded (stale) response exists for the given cache key.
    /// </summary>
    bool HasDegradedResponse(string cacheKey);

    /// <summary>
    /// Stores a successful response as a stale backup for future degraded scenarios.
    /// The data is kept with an extended TTL beyond the normal cache expiration.
    /// </summary>
    Task RecordSuccessfulResponseAsync<T>(string cacheKey, T value, CancellationToken ct = default) where T : class;
}
