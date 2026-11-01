using System.Collections.Concurrent;
using His.Hope.AspNetCore.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using His.Hope.Persistence;

namespace His.Hope.Persistence.Tenancy;

public interface IHisHopeDbContextFactory<TContext> where TContext : DbContext
{
    TContext CreateDbContext(string? tenantKey = null);

    Task<TContext> CreateDbContextAsync(
        string? tenantKey = null,
        CancellationToken cancellationToken = default);

    TContext CreateDbContextForConnection(string connectionName);

    Task<TContext> CreateDbContextForConnectionAsync(
        string connectionName,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> GetRegisteredConnectionNames();
}

public delegate void HisHopeDbContextOptionsConfigurator<TContext>(
    IServiceProvider serviceProvider,
    DbContextOptionsBuilder<TContext> optionsBuilder,
    string connectionString,
    string connectionName) where TContext : DbContext;

public sealed class TenantAwareDbContextFactory<TContext>(
    string serviceName,
    TenantPlacementConnectionResolver connectionResolver,
    ITenantPlacementRegistry placementRegistry,
    IServiceProvider serviceProvider,
    HisHopeDbContextOptionsConfigurator<TContext> configureOptions) : IHisHopeDbContextFactory<TContext>
    where TContext : DbContext
{
    private readonly ConcurrentDictionary<string, DbContextOptions<TContext>> _optionsCache = new();

    public TContext CreateDbContext(string? tenantKey = null) =>
        (TContext)Activator.CreateInstance(typeof(TContext), GetOptions(ResolveConnectionString(NormalizeTenantKey(tenantKey))))!;

    public Task<TContext> CreateDbContextAsync(
        string? tenantKey = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext(tenantKey));

    public TContext CreateDbContextForConnection(string connectionName) =>
        (TContext)Activator.CreateInstance(typeof(TContext), GetOptions(ResolveConnectionStringByName(connectionName)))!;

    public Task<TContext> CreateDbContextForConnectionAsync(
        string connectionName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContextForConnection(connectionName));

    public IReadOnlyList<string> GetRegisteredConnectionNames() =>
        placementRegistry.GetServiceConnectionNames(serviceName);

    private string? NormalizeTenantKey(string? tenantKey)
    {
        if (!string.IsNullOrWhiteSpace(tenantKey))
            return tenantKey.Trim();

        return HisHopeTenantScope.Current;
    }

    private string ResolveConnectionString(string? tenantKey) =>
        connectionResolver.ResolveConnectionString(serviceName, tenantKey);

    private string ResolveConnectionStringByName(string connectionName) =>
        connectionResolver.ResolveConnectionStringByName(connectionName, serviceName);

    private DbContextOptions<TContext> GetOptions(string connectionString) =>
        _optionsCache.GetOrAdd(connectionString, static (key, state) =>
        {
            var (sp, connectionName, configure) = state;
            var builder = new DbContextOptionsBuilder<TContext>();
            configure(sp, builder, key, connectionName);
            return builder.Options;
        }, (serviceProvider, connectionName: ResolveConnectionName(connectionString), configureOptions));

    private string ResolveConnectionName(string connectionString)
    {
        foreach (var name in placementRegistry.GetServiceConnectionNames(serviceName))
        {
            if (string.Equals(
                    connectionResolver.ResolveConnectionStringByName(name, serviceName),
                    connectionString,
                    StringComparison.Ordinal))
                return name;
        }

        return placementRegistry.GetServiceConnectionNames(serviceName).FirstOrDefault() ?? serviceName;
    }
}

public sealed class HisHopeDbContextFactoryAdapter<TContext>(
    IHisHopeDbContextFactory<TContext> factory) : IDbContextFactory<TContext>
    where TContext : DbContext
{
    public TContext CreateDbContext() =>
        factory.CreateDbContext(HisHopeTenantScope.Current);

    public Task<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        factory.CreateDbContextAsync(HisHopeTenantScope.Current, cancellationToken);
}

public sealed class SingleConnectionDbContextFactory<TContext>(
    IDbContextFactory<TContext> dbFactory,
    string connectionName) : IHisHopeDbContextFactory<TContext>
    where TContext : DbContext
{
    public TContext CreateDbContext(string? tenantKey = null) =>
        dbFactory.CreateDbContext();

    public Task<TContext> CreateDbContextAsync(
        string? tenantKey = null,
        CancellationToken cancellationToken = default) =>
        dbFactory.CreateDbContextAsync(cancellationToken);

    public TContext CreateDbContextForConnection(string connectionName) =>
        dbFactory.CreateDbContext();

    public Task<TContext> CreateDbContextForConnectionAsync(
        string connectionName,
        CancellationToken cancellationToken = default) =>
        dbFactory.CreateDbContextAsync(cancellationToken);

    public IReadOnlyList<string> GetRegisteredConnectionNames() => [connectionName];
}

public static class HisHopeTenantAwareDbContextFactoryExtensions
{
    public static IServiceCollection AddHisHopeTenantAwareNpgsqlDbContextFactory<TContext>(
        this IServiceCollection services,
        string serviceName,
        IConfiguration configuration,
        Action<NpgsqlDbContextOptionsBuilder>? configureNpgsql = null,
        Action<IServiceProvider, DbContextOptionsBuilder<TContext>>? configureContext = null)
        where TContext : DbContext
    {
        services.AddHisHopeDatabasePerformance(configuration);
        return services.AddHisHopeTenantAwareDbContextFactory<TContext>(
            serviceName,
            (serviceProvider, optionsBuilder, connectionString, connectionName) =>
            {
                optionsBuilder.UseHisHopeNpgsql(
                    serviceProvider,
                    configuration,
                    connectionString,
                    connectionName,
                    configureNpgsql);
                configureContext?.Invoke(serviceProvider, optionsBuilder);
            });
    }

    public static IServiceCollection AddHisHopeTenantAwareDbContextFactory<TContext>(
        this IServiceCollection services,
        string serviceName,
        HisHopeDbContextOptionsConfigurator<TContext> configureOptions)
        where TContext : DbContext
    {
        services.AddSingleton<IHisHopeDbContextFactory<TContext>>(sp =>
            new TenantAwareDbContextFactory<TContext>(
                serviceName,
                sp.GetRequiredService<TenantPlacementConnectionResolver>(),
                sp.GetRequiredService<ITenantPlacementRegistry>(),
                sp,
                configureOptions));
        services.AddSingleton<IDbContextFactory<TContext>, HisHopeDbContextFactoryAdapter<TContext>>();
        return services;
    }
}
