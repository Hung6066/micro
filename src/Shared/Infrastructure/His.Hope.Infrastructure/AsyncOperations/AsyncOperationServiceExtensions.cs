using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.Infrastructure.AsyncOperations;

/// <summary>
/// Extension methods for registering async operation support in the DI container.
/// </summary>
public static class AsyncOperationServiceExtensions
{
    /// <summary>
    /// Registers the async operation infrastructure:
    /// <list type="bullet">
    ///   <item><see cref="OperationStatusDbContext"/> with the provided connection string</item>
    ///   <item>A bounded <see cref="Channel{T}"/> for background work items</item>
    ///   <item><see cref="AsyncOperationProcessor"/> as a hosted service</item>
    ///   <item><see cref="AsyncOperationHandlerResolver"/> as a singleton</item>
    /// </list>
    /// </summary>
    /// <param name="connectionString">The database connection string for the operation_status table.</param>
    /// <param name="channelCapacity">Maximum number of queued operations (default 100).</param>
    public static IServiceCollection AddAsyncOperationSupport(
        this IServiceCollection services,
        string connectionString,
        int channelCapacity = 100)
    {
        // Register the DbContext
        services.AddDbContext<OperationStatusDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history_operations", "public");
            }));

        // Create a bounded channel for backpressure
        var channel = Channel.CreateBounded<AsyncOperationWorkItem>(new BoundedChannelOptions(channelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        services.AddSingleton(channel);
        services.AddSingleton(channel.Reader);
        services.AddSingleton(channel.Writer);

        // Register the background processor
        services.AddHostedService<AsyncOperationProcessor>();

        // Register the handler resolver
        services.AddSingleton<AsyncOperationHandlerResolver>();

        return services;
    }

    /// <summary>
    /// Registers an <see cref="IAsyncOperationHandler{TRequest,TResult}"/> for the
    /// specified operation type, making it discoverable by the
    /// <see cref="AsyncOperationHandlerResolver"/>.
    /// </summary>
    /// <typeparam name="THandler">The concrete handler type.</typeparam>
    /// <typeparam name="TRequest">The request DTO type.</typeparam>
    /// <typeparam name="TResult">The result DTO type.</typeparam>
    /// <param name="operationType">The operation type discriminator (e.g. "PatientImport").</param>
    public static IServiceCollection AddAsyncOperationHandler<THandler, TRequest, TResult>(
        this IServiceCollection services,
        string operationType)
        where THandler : class, IAsyncOperationHandler<TRequest, TResult>
        where TRequest : class
        where TResult : class
    {
        services.AddScoped<IAsyncOperationHandler<TRequest, TResult>, THandler>();

        // Register with the resolver using a factory that resolves from DI
        services.AddSingleton(sp =>
        {
            var resolver = sp.GetRequiredService<AsyncOperationHandlerResolver>();
            // Defer instance resolution — the resolver will get it from DI at execution time
            resolver.Register<TRequest, TResult>(operationType);
            return resolver; // Already registered as singleton, but we need the side effect
        });

        return services;
    }
}
