using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace His.Hope.AgentHarness.Infrastructure.Persistence;

public sealed class HarnessDbContextFactory : IDesignTimeDbContextFactory<HarnessDbContext>
{
    public HarnessDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AgentHarness__DatabaseConnectionString")
            ?? "Host=localhost;Port=5433;Database=harness;Username=harness;Password=harness";

        var optionsBuilder = new DbContextOptionsBuilder<HarnessDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsHistoryTable("__ef_migrations_history", "harness");
            npgsql.EnableRetryOnFailure(3);
            npgsql.UseVector();
        });

        return new HarnessDbContext(optionsBuilder.Options);
    }
}
