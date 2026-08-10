namespace His.Hope.Infrastructure.Caching;

/// <summary>
/// Builds Redis patterns for cache keys that may be authorization-partitioned.
/// </summary>
public static class CacheKeyPattern
{
    public static string[] ForPrefix(string instancePrefix, string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instancePrefix);
        ArgumentNullException.ThrowIfNull(prefix);

        return
        [
            $"{instancePrefix}{prefix}*",
            $"{instancePrefix}authz-cache:*:{prefix}*",
        ];
    }
}
