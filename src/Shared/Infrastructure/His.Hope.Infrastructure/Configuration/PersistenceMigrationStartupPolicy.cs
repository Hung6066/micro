using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace His.Hope.Infrastructure.Configuration;

public static class PersistenceMigrationStartupPolicy
{
    public static bool ShouldRunMigrations(
        IConfiguration configuration,
        IHostEnvironment environment,
        string serviceName)
    {
        var runMigrationsOnStartup = configuration.GetValue<bool?>("Persistence:RunMigrationsOnStartup");
        var migrationOnly = configuration.GetValue<bool?>("Persistence:MigrationOnly");

        if (runMigrationsOnStartup is true || migrationOnly is true)
            return true;

        if (runMigrationsOnStartup is null && migrationOnly is null &&
            !environment.IsDevelopment() &&
            !string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{serviceName} requires Persistence:RunMigrationsOnStartup or Persistence:MigrationOnly outside Development.");
        }

        return false;
    }
}
