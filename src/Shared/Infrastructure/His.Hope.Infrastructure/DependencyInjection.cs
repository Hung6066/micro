using His.Hope.Infrastructure.Abuse;
using His.Hope.Infrastructure.Audit;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.Database;
using His.Hope.Infrastructure.Events;
using His.Hope.Infrastructure.Locking;
using His.Hope.Infrastructure.Middleware;
using His.Hope.Infrastructure.Observability;
using His.Hope.Infrastructure.Outbox;
using His.Hope.Infrastructure.Qos;
using His.Hope.Infrastructure.Resilience;
using MediatR;
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

        // Register hybrid cache (L1 in-memory + L2 Redis) with stampede prevention.
        // Replaces the basic distributed (L2-only) cache.
        services.AddHisHopeHybridCaching(redisConnectionString);

        // Register cache warmup background service.
        // Individual services register their IWarmupTask implementations
        // to pre-load reference data at startup.
        services.AddHostedService<CacheWarmupService>();

        // SECURITY: Register PHI audit service for HIPAA audit compliance
        services.AddPhiAudit();

        services.AddSingleton<EventTypeRegistry>();
        services.AddScoped<CorrelationContext>();
        services.AddSingleton<GlobalExceptionMiddleware>();

        // Locking pipeline behavior registered before tracing so it wraps externally
        services.AddSingleton<ILockManager, RedisLockManager>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LockingPipelineBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TracingBehaviour<,>));

        // SECURITY: Register brute force protection for login attempt tracking
        services.AddSingleton<IBruteForceProtectionService, BruteForceProtectionService>();

        // QoS: 5-tier request priority admission control
        services.AddSingleton<PriorityAdmissionMiddleware>();
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var options = new PriorityAdmissionOptions();
            config.GetSection("PriorityAdmission").Bind(options);
            return options;
        });

        return services;
    }

    public static IServiceCollection AddResiliencePolicies(
        this IServiceCollection services,
        Action<ResilienceConfiguration>? configure = null)
    {
        var config = new ResilienceConfiguration();
        configure?.Invoke(config);
        services.AddSingleton(config);
        services.AddSingleton<IResiliencePipelineFactory>(config);

        services.AddTransient<GrpcResilienceHandler>(sp =>
        {
            var factory = sp.GetRequiredService<IResiliencePipelineFactory>();
            return new GrpcResilienceHandler(factory.GetGrpcPipeline("grpc-default"));
        });

        return services;
    }
}
