using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using His.Hope.AgentHarness.Core.Interfaces;

namespace His.Hope.AgentHarness.Infrastructure.Persistence;

public static class PersistenceDependencyInjection
{
    public static IServiceCollection AddHarnessPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<HarnessDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "harness");
                npgsql.EnableRetryOnFailure(3);
                npgsql.UseVector();
            }));
        services.AddScoped<IStateStore, StateStore>();
        return services;
    }
}
