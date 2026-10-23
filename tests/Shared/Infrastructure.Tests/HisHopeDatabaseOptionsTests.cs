using His.Hope.Persistence;
using His.Hope.Persistence.Querying;
using His.Hope.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;

namespace His.Hope.Infrastructure.Tests;

public sealed class HisHopeDatabaseOptionsTests
{
    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(2, 25, 2, 25)]
    [InlineData(1, 999, 1, 200)]
    public void HisHopePage_normalizes_page_and_size(int page, int size, int expectedPage, int expectedSize)
    {
        var result = HisHopePage.Create(page, size);

        result.Number.Should().Be(expectedPage);
        result.Size.Should().Be(expectedSize);
    }

    [Fact]
    public void Pagination_defaults_are_shared_and_stable()
    {
        HisHopePaginationDefaults.FirstPage.Should().Be(1);
        HisHopePaginationDefaults.DefaultPageSize.Should().Be(50);
        HisHopePaginationDefaults.QualityDefaultPageSize.Should().Be(25);
        HisHopePaginationDefaults.ExportDefaultPageSize.Should().Be(500);
        HisHopePaginationDefaults.SmallDefaultPageSize.Should().Be(100);
        HisHopePaginationDefaults.MaxPageSize.Should().Be(200);
        HisHopePaginationDefaults.ExportMaxPageSize.Should().Be(5000);
    }

    [Fact]
    public void AddHisHopeDatabasePerformance_binds_and_registers_shared_interceptor()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:SlowQueryThresholdMilliseconds"] = "250"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHisHopeDatabasePerformance(configuration);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<HisHopeDatabasePerformanceInterceptor>().Should().NotBeNull();
        provider.GetRequiredService<IOptions<HisHopeDatabasePerformanceOptions>>().Value
            .SlowQueryThresholdMilliseconds.Should().Be(250);
    }

    [Fact]
    public void UseHisHopeNpgsql_applies_safe_production_defaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TestDb"] = "Host=localhost;Database=test;Username=app;Password=secret",
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["Database:MaxPoolSize"] = "40",
                ["Database:CommandTimeoutSeconds"] = "45",
                ["Database:DefaultQueryTrackingBehavior"] = "NoTracking"
            })
            .Build();
        var options = new DbContextOptionsBuilder()
            .UseHisHopeNpgsql(configuration, "TestDb")
            .Options;

        var core = options.Extensions.OfType<CoreOptionsExtension>().Single();
        core.QueryTrackingBehavior.Should().Be(QueryTrackingBehavior.NoTracking);
        core.IsSensitiveDataLoggingEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task UseHisHopeNpgsql_registers_vault_credential_refresh_for_each_connection_open()
    {
        var configuredConnectionString = "Host=localhost;Database=identity;Username=bootstrap;Password=bootstrap";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDb"] = configuredConnectionString
            })
            .Build();
        var resolver = new Mock<IVaultDatabaseConnectionStringResolver>();
        resolver.Setup(item => item.Resolve(configuredConnectionString, "IdentityDb"))
            .Returns("Host=localhost;Database=identity;Username=leased;Password=leased");
        using var provider = new ServiceCollection()
            .AddSingleton(resolver.Object)
            .BuildServiceProvider();

        var options = new DbContextOptionsBuilder()
            .UseHisHopeNpgsql(provider, configuration, "IdentityDb")
            .Options;
        var interceptors = options.Extensions
            .OfType<CoreOptionsExtension>()
            .Single()
            .Interceptors ?? Array.Empty<IInterceptor>();

        var refreshInterceptor = interceptors
            .OfType<DbConnectionInterceptor>()
            .SingleOrDefault(interceptor =>
                interceptor.GetType().Name == "VaultDatabaseCredentialRefreshInterceptor");
        refreshInterceptor.Should().NotBeNull();

        using var connection = new NpgsqlConnection(configuredConnectionString);
        refreshInterceptor!.ConnectionOpening(connection, null!, default);

        var connectionSettings = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        connectionSettings.Username.Should().Be("leased");
        connectionSettings.Password.Should().Be("leased");

        await using var asyncConnection = new NpgsqlConnection(configuredConnectionString);
        await refreshInterceptor.ConnectionOpeningAsync(asyncConnection, null!, default);
        var asyncConnectionSettings = new NpgsqlConnectionStringBuilder(asyncConnection.ConnectionString);
        asyncConnectionSettings.Username.Should().Be("leased");
        asyncConnectionSettings.Password.Should().Be("leased");
        resolver.Verify(item => item.Resolve(configuredConnectionString, "IdentityDb"), Times.Exactly(3));
    }
}
