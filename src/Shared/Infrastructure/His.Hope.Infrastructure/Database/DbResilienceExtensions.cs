using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.Infrastructure.Database;

public static class DbResilienceExtensions
{
    public static DbContextOptionsBuilder EnableDbResilience(
        this DbContextOptionsBuilder builder,
        int maxRetryCount = 5,
        int maxRetryDelaySeconds = 30)
    {
        if (builder.Options.FindExtension<Microsoft.EntityFrameworkCore.Infrastructure.SqlServerOptionsExtension>() is not null)
        {
            builder.UseSqlServer(options =>
            {
                options.EnableRetryOnFailure(
                    maxRetryCount: maxRetryCount,
                    maxRetryDelay: TimeSpan.FromSeconds(maxRetryDelaySeconds),
                    errorNumbersToAdd: null);
            });
        }

        return builder;
    }

    public static IServiceCollection AddDbHealthCheck<TDbContext>(
        this IServiceCollection services,
        string name = "database")
        where TDbContext : DbContext
    {
        services.AddHealthChecks()
            .AddDbContextCheck<TDbContext>(
                name: name,
                tags: ["database", "sql"]);

        return services;
    }
}
