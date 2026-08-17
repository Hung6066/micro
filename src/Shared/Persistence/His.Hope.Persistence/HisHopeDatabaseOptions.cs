using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using System.Data.Common;

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

        var builder = new NpgsqlConnectionStringBuilder(NormalizePostgreSqlConnectionString(connectionString))
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

    /// <summary>
    /// Runtime contracts use PostgreSQL URIs so Docker, VM and Kubernetes can
    /// share one environment format. Npgsql's connection-string builder only
    /// accepts key/value syntax, so normalize the URI at the persistence seam.
    /// A compose suffix such as <c>;Password=...</c> is also accepted.
    /// </summary>
    private static string NormalizePostgreSqlConnectionString(string connectionString)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals("postgresql", StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals("postgres", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var rawBase = connectionString;
        var suffix = string.Empty;
        var suffixIndex = connectionString.IndexOf(';');
        if (suffixIndex >= 0)
        {
            rawBase = connectionString[..suffixIndex];
            suffix = connectionString[(suffixIndex + 1)..];
            uri = new Uri(rawBase, UriKind.Absolute);
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
        };

        if (!string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            var credentials = Uri.UnescapeDataString(uri.UserInfo).Split(':', 2);
            builder.Username = credentials[0];
            if (credentials.Length == 2)
                builder.Password = credentials[1];
        }

        foreach (var pair in ParseConnectionStringPairs(suffix))
            builder[pair.Key] = pair.Value;

        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var item in query)
        {
            var parts = item.Split('=', 2);
            if (parts.Length == 2)
                builder[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
        }

        return builder.ConnectionString;
    }

    private static IEnumerable<KeyValuePair<string, string>> ParseConnectionStringPairs(string suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix))
            yield break;

        var parsed = new DbConnectionStringBuilder { ConnectionString = suffix };
        foreach (var entry in parsed.Cast<KeyValuePair<string, object>>())
            yield return new KeyValuePair<string, string>(entry.Key, Convert.ToString(entry.Value)!);
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
