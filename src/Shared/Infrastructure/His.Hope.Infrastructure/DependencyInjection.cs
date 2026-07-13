using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.Database;
using His.Hope.Infrastructure.Observability;
using His.Hope.Infrastructure.Outbox;
using His.Hope.Infrastructure.Resilience;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddHisHopeEnterpriseInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        string redisConnectionString = "localhost:6379")
    {
        services.AddHisHopeOpenTelemetry(configuration, serviceName);
        services.AddHisHopeCaching(redisConnectionString);

        return services;
    }

    public static IServiceCollection AddResiliencePolicies(
        this IServiceCollection services,
        Action<ResilienceConfiguration>? configure = null)
    {
        var config = new ResilienceConfiguration();
        configure?.Invoke(config);
        services.AddSingleton(config);

        services.AddSingleton(sp =>
        {
            var cfg = sp.GetRequiredService<ResilienceConfiguration>();
            return cfg;
        });

        return services;
    }
}
