using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace His.Hope.Persistence;

public interface IMigrationRunner
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
}

public interface IDbMigrationRunner
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
}

public sealed class EfCoreMigrationRunner<TDbContext>(TDbContext dbContext, ILogger<EfCoreMigrationRunner<TDbContext>> logger) : IMigrationRunner, IDbMigrationRunner
    where TDbContext : DbContext
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Applying EF Core migrations for {DbContext}", typeof(TDbContext).Name);
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        var lockKey = $"his-hope:migrations:{typeof(TDbContext).FullName}";

        try
        {
            await ExecuteAdvisoryLockAsync(connection, lockKey, cancellationToken);
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        finally
        {
            await ExecuteAdvisoryUnlockAsync(connection, lockKey, CancellationToken.None);
            await connection.CloseAsync();
        }

        logger.LogInformation("EF Core migrations completed for {DbContext}", typeof(TDbContext).Name);
    }

    private static async Task ExecuteAdvisoryLockAsync(
        System.Data.Common.DbConnection connection,
        string lockKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_advisory_lock(hashtextextended(@lock_key, 0));";
        AddParameter(command, "@lock_key", lockKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteAdvisoryUnlockAsync(
        System.Data.Common.DbConnection connection,
        string lockKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_advisory_unlock(hashtextextended(@lock_key, 0));";
        AddParameter(command, "@lock_key", lockKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

public sealed class CompositeMigrationRunner(IEnumerable<IDbMigrationRunner> runners) : IMigrationRunner
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        foreach (var runner in runners)
        {
            await runner.MigrateAsync(cancellationToken);
        }
    }
}

public static class MigrationRunnerExtensions
{
    public static IServiceCollection AddHisHopeMigrationRunner<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddScoped<IDbMigrationRunner, EfCoreMigrationRunner<TDbContext>>();
        services.AddScoped<CompositeMigrationRunner>();
        services.AddScoped<IMigrationRunner>(sp => sp.GetRequiredService<CompositeMigrationRunner>());
        return services;
    }
}
