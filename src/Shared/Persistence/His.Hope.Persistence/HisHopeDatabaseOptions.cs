using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    public static IServiceCollection AddHisHopeDatabasePerformance(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<HisHopeDatabasePerformanceOptions>()
            .Bind(configuration.GetSection("Database"))
            .Validate(options => options.SlowQueryThresholdMilliseconds >= 0,
                "Database:SlowQueryThresholdMilliseconds must be zero or greater")
            .ValidateOnStart();
        services.AddSingleton<HisHopeDatabasePerformanceInterceptor>();
        return services;
    }

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

        var environmentName = configuration["HIS_HOPE_ENVIRONMENT"]
            ?? configuration["ASPNETCORE_ENVIRONMENT"];
        if (string.Equals(environmentName, "production", StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(builder.Password) ||
             builder.Password.Equals("postgres", StringComparison.OrdinalIgnoreCase) ||
             builder.Password.Equals("password", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Production database connection '{connectionName}' must use a non-default credential supplied by a secret provider.");
        }

        options.UseNpgsql(builder.ConnectionString, npgsql =>
        {
            npgsql.MaxBatchSize(database.GetValue("MaxBatchSize", 100));
            npgsql.EnableRetryOnFailure(
                database.GetValue("MaxRetryCount", 5),
                TimeSpan.FromSeconds(database.GetValue("MaxRetryDelaySeconds", 30)),
                errorCodesToAdd: null);
            configure?.Invoke(npgsql);
        });

        options.UseQueryTrackingBehavior(ParseTrackingBehavior(database["DefaultQueryTrackingBehavior"]));
        options.EnableDetailedErrors(database.GetValue("EnableDetailedErrors", false));
        options.EnableSensitiveDataLogging(
            database.GetValue("EnableSensitiveDataLogging", false) &&
            !string.Equals(environmentName, "production", StringComparison.OrdinalIgnoreCase));

        return options;
    }

    public static DbContextOptionsBuilder UseHisHopeDatabasePerformance(
        this DbContextOptionsBuilder options,
        IServiceProvider serviceProvider)
    {
        var interceptor = serviceProvider.GetService<HisHopeDatabasePerformanceInterceptor>();
        if (interceptor is not null)
            options.AddInterceptors(interceptor);
        return options;
    }

    private static QueryTrackingBehavior ParseTrackingBehavior(string? value) =>
        Enum.TryParse<QueryTrackingBehavior>(value, ignoreCase: true, out var behavior)
            ? behavior
            : QueryTrackingBehavior.TrackAll;

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
        string connectionString,
        string connectionName,
        Action<NpgsqlDbContextOptionsBuilder>? configure = null)
    {
        var resolver = serviceProvider.GetService<His.Hope.Secrets.IVaultDatabaseConnectionStringResolver>();
        var resolved = resolver?.Resolve(connectionString, connectionName) ?? connectionString;
        return UseHisHopeNpgsql(options, resolved, connectionName, configuration, configure)
            .UseHisHopeDatabasePerformance(serviceProvider);
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

public sealed class HisHopeDatabasePerformanceOptions
{
    public int SlowQueryThresholdMilliseconds { get; set; } = 500;
}

public sealed class HisHopeDatabasePerformanceInterceptor(
    ILogger<HisHopeDatabasePerformanceInterceptor> logger,
    Microsoft.Extensions.Options.IOptions<HisHopeDatabasePerformanceOptions> options) : DbCommandInterceptor
{
    private readonly int _thresholdMilliseconds = options.Value.SlowQueryThresholdMilliseconds;

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        LogSlowQuery(command, eventData.Duration);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        LogSlowQuery(command, eventData.Duration);
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        LogSlowQuery(command, eventData.Duration);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        LogSlowQuery(command, eventData.Duration);
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        LogSlowQuery(command, eventData.Duration);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        LogSlowQuery(command, eventData.Duration);
        return ValueTask.FromResult(result);
    }

    private void LogSlowQuery(DbCommand command, TimeSpan duration)
    {
        if (_thresholdMilliseconds <= 0 || duration.TotalMilliseconds < _thresholdMilliseconds)
            return;

        logger.LogWarning(
            "Slow database command detected. UseCase={UseCase} DurationMs={DurationMs} CommandType={CommandType} ParameterCount={ParameterCount}",
            ResolveUseCase(command), Math.Round(duration.TotalMilliseconds, 1), command.CommandType, command.Parameters.Count);
    }

    private static string ResolveUseCase(DbCommand command)
    {
        foreach (var line in command.CommandText.Split('\n'))
        {
            var comment = line.Trim();
            const string prefix = "-- HisHope.UseCase:";
            if (comment.StartsWith(prefix, StringComparison.Ordinal))
                return comment[prefix.Length..].Trim();
        }

        return "unknown";
    }
}
