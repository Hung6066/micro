using FluentAssertions;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.ManufacturingService.Infrastructure.Persistence;
using His.Hope.Infrastructure.DataLifecycle;
using His.Hope.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class TenantPlacementRoutingTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string _sharedConnection = string.Empty;
    private string _dedicatedConnection = string.Empty;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("manufacturing_shared")
            .WithUsername("testuser")
            .WithPassword("testpass123!")
            .WithCleanUp(true)
            .Build();
        await _container.StartAsync();
        _sharedConnection = _container.GetConnectionString();
        _dedicatedConnection = new Npgsql.NpgsqlConnectionStringBuilder(_sharedConnection)
        {
            Database = "manufacturing_dedicated"
        }.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    [Fact]
    public async Task Dedicated_tenant_routes_to_alternate_database_when_placement_enabled()
    {
        const string sharedTenant = "manufacturing";
        const string dedicatedTenant = "customer-enterprise-y";

        var registry = new TenantPlacementRegistry(
            Microsoft.Extensions.Options.Options.Create(new TenantPlacementOptions
            {
                Enabled = true,
                Services =
                {
                    ["manufacturing"] = new TenantPlacementServiceOptions { DefaultConnectionName = "ManufacturingDb" }
                },
                Placements =
                {
                    new TenantPlacementEntryOptions
                    {
                        TenantKey = dedicatedTenant,
                        Tier = TenantPlacementTier.Dedicated,
                        Active = true,
                        Services =
                        {
                            ["manufacturing"] = new TenantPlacementServiceBindingOptions
                            {
                                ConnectionName = "ManufacturingDb_customer_enterprise_y"
                            }
                        }
                    }
                }
            }),
            new TestHostEnvironment(),
            NullLogger<TenantPlacementRegistry>.Instance);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ManufacturingDb"] = _sharedConnection,
                ["ConnectionStrings:ManufacturingDb_customer_enterprise_y"] = _dedicatedConnection
            })
            .Build();

        var resolver = new TenantPlacementConnectionResolver(registry, configuration);
        var interceptor = new SoftDeleteInterceptor(() => null);
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var factory = new TenantAwareDbContextFactory<ManufacturingDbContext>(
            "manufacturing",
            resolver,
            registry,
            serviceProvider,
            (_, builder, connectionString, _) =>
                builder.UseNpgsql(connectionString).AddInterceptors(interceptor));

        await using (var sharedDb = await factory.CreateDbContextForConnectionAsync("ManufacturingDb"))
            await sharedDb.Database.MigrateAsync();

        await using (var admin = new Npgsql.NpgsqlConnection(_sharedConnection))
        {
            await admin.OpenAsync();
            await using var terminate = new Npgsql.NpgsqlCommand(
                """
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = 'manufacturing_shared' AND pid <> pg_backend_pid()
                """, admin);
            await terminate.ExecuteNonQueryAsync();
            await using var clone = new Npgsql.NpgsqlCommand(
                """
                SELECT 1 FROM pg_database WHERE datname = 'manufacturing_dedicated';
                """, admin);
            if (await clone.ExecuteScalarAsync() is null)
            {
                await using var create = new Npgsql.NpgsqlCommand(
                    "CREATE DATABASE manufacturing_dedicated TEMPLATE manufacturing_shared", admin);
                await create.ExecuteNonQueryAsync();
            }
        }

        await using (var sharedDb = factory.CreateDbContext(sharedTenant))
        {
            (await sharedDb.Database.SqlQueryRaw<string>("SELECT current_database() AS \"Value\"")
                .SingleAsync()).Should().Be("manufacturing_shared");
        }

        await using (var dedicatedDb = factory.CreateDbContext(dedicatedTenant))
        {
            (await dedicatedDb.Database.SqlQueryRaw<string>("SELECT current_database() AS \"Value\"")
                .SingleAsync()).Should().Be("manufacturing_dedicated");
        }

        await using (var verifyShared = factory.CreateDbContextForConnection("ManufacturingDb"))
        {
            (await verifyShared.Database.SqlQueryRaw<string>("SELECT current_database() AS \"Value\"")
                .SingleAsync()).Should().Be("manufacturing_shared");
        }

        await using (var verifyDedicated = factory.CreateDbContextForConnection("ManufacturingDb_customer_enterprise_y"))
        {
            (await verifyDedicated.Database.SqlQueryRaw<string>("SELECT current_database() AS \"Value\"")
                .SingleAsync()).Should().Be("manufacturing_dedicated");
        }
    }

    private sealed class TestHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Microsoft.Extensions.Hosting.Environments.Development;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
