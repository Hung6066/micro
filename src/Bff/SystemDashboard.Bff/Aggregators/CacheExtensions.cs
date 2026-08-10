using Microsoft.Extensions.Caching.Memory;

namespace SystemDashboard.Bff.Aggregators;

public static class CacheExtensions
{
    public static async Task<T?> GetOrCreateAsync<T>(
        this IMemoryCache cache,
        string key,
        Func<Task<T>> factory,
        TimeSpan absoluteExpiration)
    {
        if (cache.TryGetValue(key, out T? cached) && cached is not null)
            return cached;

        var result = await factory();
        if (result is not null)
            cache.Set(key, result, absoluteExpiration);
        return result;
    }
}
