using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace His.Hope.Persistence;

/// <summary>
/// Common PostgreSQL policy for service-owned databases. Individual services
/// still own their DbContext and migrations; this module owns the cross-cutting
/// connection and retry contract.
/// </summary>
public static class HisHopeDatabaseOptions
{
    public static DbContextOptionsBuilder UseHisHopeNpgsql(
        this DbContextOptionsBuilder options,
        IConfiguration configuration,
        string connectionName,
        Action<NpgsqlDbContextOptionsBuilder>? configure = null)
    {
        var connectionString = configuration.GetConnectionString(connectionName)
            ?? throw new InvalidOperationException($"Connection string '{connectionName}' is required.");
        return UseHisHopeNpgsql(options, connectionString, connectionName, configuration, configure);
    }

    private static DbContextOptionsBuilder UseHisHopeNpgsql(
        DbContextOptionsBuilder options,
        string connectionString,
        string connectionName,
        IConfiguration configuration,
        Action<NpgsqlDbContextOptionsBuilder>? configure)
    {
        var database = configuration.GetSection("Database");
        var serviceName = configuration.GetValue<string>("ServiceName")
            ?? configuration.GetValue<string>("SERVICE_NAME");

        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            ApplicationName = serviceName ?? connectionName,
            Timeout = database.GetValue("ConnectionTimeoutSeconds", 15),
            CommandTimeout = database.GetValue("CommandTimeoutSeconds", 30),
            KeepAlive = database.GetValue("KeepAliveSeconds", 30),
            MinPoolSize = database.GetValue("MinPoolSize", 0),
            MaxPoolSize = database.GetValue("MaxPoolSize", 20),
            ConnectionLifetime = database.GetValue("ConnectionLifetimeSeconds", 60),
            ConnectionIdleLifetime = database.GetValue("ConnectionIdleLifetimeSeconds", 30)
        };

        options.UseNpgsql(builder.ConnectionString, npgsql =>
        {
            npgsql.EnableRetryOnFailure(
                database.GetValue("MaxRetryCount", 5),
                TimeSpan.FromSeconds(database.GetValue("MaxRetryDelaySeconds", 30)),
                errorCodesToAdd: null);
            configure?.Invoke(npgsql);
        });

        return options;
    }

    public static DbContextOptionsBuilder UseHisHopeNpgsql(
        this DbContextOptionsBuilder options,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        string connectionName,
        Action<NpgsqlDbContextOptionsBuilder>? configure = null)
    {
        var configured = configuration.GetConnectionString(connectionName)
            ?? throw new InvalidOperationException($"Connection string '{connectionName}' is required.");
        var resolver = serviceProvider.GetService<His.Hope.Secrets.IVaultDatabaseConnectionStringResolver>();
        var resolved = resolver?.Resolve(configured, connectionName) ?? configured;
        return UseHisHopeNpgsql(options, resolved, connectionName, configuration, configure);
    }
}
