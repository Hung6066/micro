using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.Infrastructure.Caching;

public static class CacheServiceExtensions
{
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
}
