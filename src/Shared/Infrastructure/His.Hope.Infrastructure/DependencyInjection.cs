using His.Hope.Infrastructure.Audit;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.Database;
using His.Hope.Infrastructure.Events;
using His.Hope.Infrastructure.Locking;
using His.Hope.Infrastructure.Middleware;
using His.Hope.Infrastructure.Observability;
using His.Hope.Infrastructure.Outbox;
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
        services.AddHisHopeCaching(redisConnectionString);

        // SECURITY: Register PHI audit service for HIPAA audit compliance
        services.AddPhiAudit();

        services.AddSingleton<EventTypeRegistry>();
        services.AddScoped<CorrelationContext>();
        services.AddSingleton<GlobalExceptionMiddleware>();

        // Locking pipeline behavior registered before tracing so it wraps externally
        services.AddSingleton<ILockManager, RedisLockManager>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LockingPipelineBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TracingBehaviour<,>));

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
