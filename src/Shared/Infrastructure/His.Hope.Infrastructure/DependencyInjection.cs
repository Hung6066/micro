using His.Hope.Infrastructure.Abuse;
using His.Hope.Infrastructure.Backpressure;
using His.Hope.Infrastructure.Audit;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.Database;
using His.Hope.Infrastructure.Degradation;
using His.Hope.Infrastructure.Events;
using His.Hope.Infrastructure.FeatureFlags;
using His.Hope.Infrastructure.Locking;
using His.Hope.Infrastructure.Middleware;
using His.Hope.Infrastructure.Observability;
using His.Hope.Infrastructure.Outbox;
using His.Hope.Infrastructure.Qos;
using His.Hope.Infrastructure.Resilience;
using His.Hope.Infrastructure.Security;
using His.Hope.Messaging.RabbitMq;
using His.Hope.Messaging.Redis;
using His.Hope.Messaging.Sql;
using His.Hope.Messaging;
using His.Hope.Observability.OpenTelemetry;
using His.Hope.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace His.Hope.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddHisHopeEnterpriseInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        string redisConnectionString = "localhost:6379")
    {
        services.AddHisHopeOpenTelemetryExporters(configuration, serviceName);
        services.AddFeatureFlags(configuration);

        // Register hybrid cache (L1 in-memory + L2 Redis) with stampede prevention.
        // Replaces the basic distributed (L2-only) cache.
        // Register singleton Redis ConnectionMultiplexer for lock manager and cache
        // Uses default settings matching the health check's ConnectionMultiplexer
        services.AddSingleton<IConnectionMultiplexer>(sp =>
            RedisConnectionFactory.Connect(redisConnectionString, configuration));
        // Keep the platform contract compatible with components that consume
        // Microsoft.Extensions.Caching.Distributed (token blacklist and
        // brute-force protection). The custom hybrid cache above uses the same
        // connection multiplexer, while this adapter supplies the standard
        // IDistributedCache abstraction to every service.
        services.AddStackExchangeRedisCache(options =>
        {
            options.ConfigurationOptions = RedisConnectionFactory.CreateOptions(redisConnectionString, configuration);
            options.InstanceName = $"HisHope:{serviceName}:";
        });
        services.AddHisHopeRedisMessaging();
        services.AddHisHopeRabbitMqMessaging(configuration);

        if (configuration.GetValue<bool>("Messaging:Sql:Enabled"))
        {
            var messagingConnection = configuration.GetConnectionString("Messaging")
                ?? configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Messaging SQL is enabled but no Messaging or DefaultConnection connection string exists.");
            services.AddHisHopeSqlMessaging(options => options.UseNpgsql(
                messagingConnection,
                npgsql => npgsql.MigrationsAssembly(typeof(SqlMessagingDbContext).Assembly.GetName().Name)));
            services.AddHisHopeMigrationRunner<SqlMessagingDbContext>();
        }

        services.AddHisHopeHybridCaching(redisConnectionString);
        services.AddSingleton<DpopResourceProofValidator>();

        // Register cache warmup background service.
        // Individual services register their IWarmupTask implementations
        // to pre-load reference data at startup.
        services.AddHostedService<CacheWarmupService>();

        // Graceful degradation: stale cache fallback service.
        // Provides stale cached data when downstream systems fail.
        // Uses IHttpContextAccessor to set X-Degraded-Data response header.
        services.AddHttpContextAccessor();
        services.AddSingleton<AuthorizationCacheKeyPartitioner>();
        services.AddSingleton<IDegradedResponseProvider, StaleCacheFallbackPolicy>();

        // SECURITY: Register PHI audit service for HIPAA audit compliance
        services.AddPhiAudit();

        services.AddSingleton<EventTypeRegistry>();
        // P2 schema compatibility seam. Individual bounded contexts register
        // their event versions; consumers can reject incompatible payloads
        // before side effects are executed.
        services.AddSingleton<EventSchemaRegistry>();
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection("Outbox"))
            .PostConfigure(options => options.Validate());
        services.AddScoped<CorrelationContext>();
        // Locking pipeline behavior registered before tracing so it wraps externally
        services.AddSingleton<ILockManager, RedisLockManager>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LockingPipelineBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TracingBehaviour<,>));

        // SECURITY: Register brute force protection for login attempt tracking
        services.AddSingleton<IBruteForceProtectionService, BruteForceProtectionService>();

        // QoS: 5-tier request priority admission control
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
        // Adaptive concurrency limiter: self-tunes max parallelism based on
        // observed p99 latency. Must be singleton to maintain rolling window.
        services.AddSingleton<AdaptiveConcurrencyLimiterRegistry>();

        services.AddSingleton(sp =>
        {
            var registry = sp.GetRequiredService<AdaptiveConcurrencyLimiterRegistry>();
            var config = new ResilienceConfiguration(registry);
            configure?.Invoke(config);
            return config;
        });
        services.AddSingleton<IResiliencePipelineFactory>(sp =>
            sp.GetRequiredService<ResilienceConfiguration>());

        services.AddTransient<GrpcResilienceHandler>(sp =>
        {
            var factory = sp.GetRequiredService<IResiliencePipelineFactory>();
            return new GrpcResilienceHandler(factory.GetGrpcPipeline("grpc-default"));
        });

        return services;
    }
}
