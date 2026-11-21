using His.Hope.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace His.Hope.Infrastructure.Tests;

public sealed class PersistenceMigrationStartupPolicyTests
{
    [Fact]
    public void ProductionAllowsExplicitFalseForApiRuntimeWithoutRunningMigrations()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Persistence:RunMigrationsOnStartup"] = "false"
        });

        var runMigrations = PersistenceMigrationStartupPolicy.ShouldRunMigrations(
            configuration,
            CreateEnvironment(Environments.Production),
            "CommerceService");

        Assert.False(runMigrations);
    }

    [Fact]
    public void ProductionStillFailsWhenBothMigrationSettingsAreMissing()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>());

        var error = Assert.Throws<InvalidOperationException>(() =>
            PersistenceMigrationStartupPolicy.ShouldRunMigrations(
                configuration,
                CreateEnvironment(Environments.Production),
                "CommerceService"));

        Assert.Contains("CommerceService", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Persistence:RunMigrationsOnStartup", "true")]
    [InlineData("Persistence:MigrationOnly", "true")]
    public void ProductionRunsMigrationsWhenEitherExplicitMigrationModeIsEnabled(string key, string value)
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            [key] = value
        });

        var runMigrations = PersistenceMigrationStartupPolicy.ShouldRunMigrations(
            configuration,
            CreateEnvironment(Environments.Production),
            "CommerceService");

        Assert.True(runMigrations);
    }

    [Fact]
    public void DevelopmentAllowsMissingMigrationSettings()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>());

        var runMigrations = PersistenceMigrationStartupPolicy.ShouldRunMigrations(
            configuration,
            CreateEnvironment(Environments.Development),
            "CommerceService");

        Assert.False(runMigrations);
    }

    private static IConfiguration CreateConfiguration(IDictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private static IHostEnvironment CreateEnvironment(string environmentName) => new TestHostEnvironment
    {
        EnvironmentName = environmentName
    };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Infrastructure.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
