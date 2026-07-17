using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace His.Hope.Infrastructure.Caching;

public static class CacheServiceExtensions
{
    /// <summary>
    /// Registers the standard Redis-based distributed cache (L2).
    /// </summary>
    public static IServiceCollection AddHisHopeCaching(
        this IServiceCollection services,
        string redisConnectionString = "localhost:6379")
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "HisHope:";
        });

        services.AddScoped<ICacheService, DistributedCacheService>();

        return services;
    }

    /// <summary>
    /// Registers the multi-level hybrid cache (L1 in-memory + L2 Redis)
    /// with XFetch-inspired stampede prevention.
    ///
    /// Replaces the default ICacheService registration with IHybridCacheService
    /// composed over both MemoryCacheService and DistributedCacheService.
    /// </summary>
    public static IServiceCollection AddHisHopeHybridCaching(
        this IServiceCollection services,
        string redisConnectionString = "localhost:6379")
    {
        // Register L2 (Redis) first
        services.AddHisHopeCaching(redisConnectionString);

        // Register L1 (in-memory) with size limit of 500 entries
        services.AddMemoryCache();
        services.AddOptions<MemoryCacheServiceOptions>();
        services.PostConfigure<MemoryCacheServiceOptions>(options =>
        {
            // Ensure size limit is enforced by MemoryCache
            options.SizeLimit = 500;
        });
        services.TryAddSingleton<IMemoryCacheService, MemoryCacheService>();

        // Register hybrid cache options
        services.AddOptions<HybridCacheOptions>();
        services.PostConfigure<HybridCacheOptions>(options =>
        {
            // Sensible defaults already set in the options class
        });

        // Register hybrid cache as both IHybridCacheService (new) and ICacheService (replacement)
        services.TryAddSingleton<IHybridCacheService, HybridCacheService>();
        services.AddSingleton<ICacheService>(sp =>
            sp.GetRequiredService<IHybridCacheService>());

        return services;
    }

    /// <summary>
    /// Registers a cache warmup task to be executed at startup.
    /// Multiple tasks are supported and run in priority order.
    /// </summary>
    public static IServiceCollection AddCacheWarmupTask<T>(this IServiceCollection services)
        where T : class, IWarmupTask
    {
        services.AddSingleton<IWarmupTask, T>();
        return services;
    }
}
