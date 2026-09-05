using His.Hope.Infrastructure.Locking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace His.Hope.Infrastructure.Saga;

/// <summary>
/// Extension methods for registering saga persistence and orchestration services.
///
/// Usage in a service's Startup/Program:
/// <code>
///   services.AddSagaPersistence&lt;TContext&gt;(connectionString);
///   services.AddSagaOrchestrator&lt;MyData&gt;()
///       .AddSagaRecoveryHandler&lt;MyData&gt;();
///   services.AddSagaRecoveryService();
/// </code>
/// </summary>
public static class SagaServiceExtensions
{
    /// <summary>
    /// Registers <see cref="SagaDbContext"/> with EF Core and registers
    /// <see cref="EfSagaStateStore"/> as the <see cref="ISagaStateStore"/> implementation.
    /// </summary>
    /// <param name="connectionString">
    /// CockroachDB connection string for the saga database.
    /// </param>
    public static IServiceCollection AddSagaPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContextFactory<SagaDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.TryAddScoped<ISagaStateStore, EfSagaStateStore>();

        return services;
    }

    public static IServiceCollection AddSagaPersistence(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configure)
    {
        services.AddDbContextFactory<SagaDbContext>((sp, options) => configure(sp, options));
        services.TryAddScoped<ISagaStateStore, EfSagaStateStore>();
        return services;
    }

    public static IServiceCollection AddSagaOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<SagaOptions>()
            .Bind(configuration.GetSection(SagaOptions.SectionName))
            .Validate(options =>
            {
                try { options.Validate(); return true; }
                catch (OptionsValidationException) { return false; }
            }, "Saga timing and batch options must be greater than zero.")
            .ValidateOnStart();
        return services;
    }

    /// <summary>
    /// Registers <see cref="PersistentSagaOrchestrator{TData}"/> as a scoped service.
    /// </summary>
    public static IServiceCollection AddSagaOrchestrator<TData>(
        this IServiceCollection services)
    {
        services.AddScoped(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SagaOptions>>().Value;
            var orchestrator = new PersistentSagaOrchestrator<TData>(
                sp.GetRequiredService<ISagaStateStore>(),
                sp.GetRequiredService<ILockManager>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PersistentSagaOrchestrator<TData>>>());
            orchestrator.PerStepTimeout = TimeSpan.FromSeconds(options.PerStepTimeoutSeconds);
            orchestrator.HeartbeatInterval = TimeSpan.FromSeconds(options.HeartbeatIntervalSeconds);
            orchestrator.LockTtl = TimeSpan.FromSeconds(options.LockTtlSeconds);
            foreach (var step in sp.GetServices<ISagaStep<TData>>())
                orchestrator.AddStep(step);
            return orchestrator;
        });
        return services;
    }

    /// <summary>
    /// Registers a <see cref="SagaRecoveryHandler{TData}"/> as an
    /// <see cref="ISagaRecoveryHandler"/> so the <see cref="SagaRecoveryService"/>
    /// can discover and recover sagas of this type.
    ///
    /// Call after <c>AddSagaOrchestrator&lt;TData&gt;()</c>.
    /// </summary>
    public static IServiceCollection AddSagaRecoveryHandler<TData>(
        this IServiceCollection services)
    {
        services.AddScoped<ISagaRecoveryHandler, SagaRecoveryHandler<TData>>();
        return services;
    }

    /// <summary>
    /// Registers the <see cref="SagaRecoveryService"/> background service
    /// that periodically scans for stale sagas and attempts recovery.
    /// </summary>
    public static IServiceCollection AddSagaRecoveryService(
        this IServiceCollection services)
    {
        services.AddHostedService<SagaRecoveryService>();
        return services;
    }
}
