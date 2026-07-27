using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace His.Hope.Persistence;

public interface IMigrationRunner
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
}

public sealed class EfCoreMigrationRunner<TDbContext>(TDbContext dbContext, ILogger<EfCoreMigrationRunner<TDbContext>> logger) : IMigrationRunner
    where TDbContext : DbContext
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Applying EF Core migrations for {DbContext}", typeof(TDbContext).Name);
        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("EF Core migrations completed for {DbContext}", typeof(TDbContext).Name);
    }
}

public static class MigrationRunnerExtensions
{
    public static IServiceCollection AddHisHopeMigrationRunner<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddScoped<IMigrationRunner, EfCoreMigrationRunner<TDbContext>>();
        return services;
    }
}
