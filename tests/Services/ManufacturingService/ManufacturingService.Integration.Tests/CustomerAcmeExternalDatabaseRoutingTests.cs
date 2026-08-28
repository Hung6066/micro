using System.Text.Json;
using FluentAssertions;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.Infrastructure.DataLifecycle;
using His.Hope.ManufacturingService.Infrastructure.Persistence;
using His.Hope.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Sdk;

/// <summary>
/// Live routing test for customer-acme dedicated Manufacturing DB (external PostgreSQL).
/// Skips unless connection strings are supplied via environment or connections JSON file.
/// </summary>
public sealed class CustomerAcmeExternalDatabaseRoutingTests
{
    private const string TenantKey = "customer-acme";
    private const string DedicatedConnectionName = "ManufacturingDb_customer_acme";
    private const string SharedConnectionName = "ManufacturingDb";
    private const string SharedTenant = "manufacturing";

    [Fact]
    public async Task Customer_acme_routes_to_external_database_when_placement_enabled()
    {
        var dedicatedConnection = ResolveDedicatedConnectionString();
        if (string.IsNullOrWhiteSpace(dedicatedConnection))
            throw SkipException.ForSkip("Set MANUFACTURING_DB_CUSTOMER_ACME_CONNECTION or TENANT_PLACEMENT_CONNECTIONS_FILE with ManufacturingDb_customer_acme.");

        var skipShared = string.Equals(
            Environment.GetEnvironmentVariable("CUSTOMER_ACME_SKIP_SHARED_ROUTING_TEST"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        var sharedConnection = ResolveSharedConnectionString();
        if (!skipShared && string.IsNullOrWhiteSpace(sharedConnection))
        {
            throw SkipException.ForSkip(
                "Shared ManufacturingDb is resolved from env (ConnectionStrings__ManufacturingDb, " +
                "MANUFACTURING_TEST_POSTGRES_CONNECTION, DATABASE_MANUFACTURING_URL) or run with " +
                "CUSTOMER_ACME_SKIP_SHARED_ROUTING_TEST=true.");
        }

        var registry = BuildRegistryFromFile();
        var connectionValues = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{DedicatedConnectionName}"] = dedicatedConnection,
        };
        if (!string.IsNullOrWhiteSpace(sharedConnection))
            connectionValues[$"ConnectionStrings:{SharedConnectionName}"] = sharedConnection;
        else if (skipShared)
            // The resolver enumerates the service's default connection while
            // deriving the configured name. Keep the shared slot resolvable
            // without requiring a second database when that probe is skipped.
            connectionValues[$"ConnectionStrings:{SharedConnectionName}"] = dedicatedConnection;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(connectionValues)
            .Build();

        var resolver = new TenantPlacementConnectionResolver(registry, configuration);
        var interceptor = new SoftDeleteInterceptor(() => null);
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var factory = new TenantAwareDbContextFactory<ManufacturingDbContext>(
            "manufacturing",
            resolver,
            registry,
            serviceProvider,
            (_, builder, connectionString, _) =>
                builder.UseNpgsql(connectionString).AddInterceptors(interceptor));

        var expectedDedicatedDatabase = GetDatabaseName(dedicatedConnection);

        await using (var dedicatedProbe = await factory.CreateDbContextAsync(TenantKey))
        {
            var canConnect = await dedicatedProbe.Database.CanConnectAsync();
            canConnect.Should().BeTrue("external dedicated database must be reachable");
            (await dedicatedProbe.Database.SqlQueryRaw<string>("SELECT current_database() AS \"Value\"")
                .SingleAsync()).Should().Be(expectedDedicatedDatabase);
        }

        if (!skipShared && !string.IsNullOrWhiteSpace(sharedConnection))
        {
            var expectedSharedDatabase = GetDatabaseName(sharedConnection);
            await using var sharedProbe = await factory.CreateDbContextAsync(SharedTenant);
            (await sharedProbe.Database.SqlQueryRaw<string>("SELECT current_database() AS \"Value\"")
                .SingleAsync()).Should().Be(expectedSharedDatabase);
        }

        await using (var byConnection = await factory.CreateDbContextForConnectionAsync(DedicatedConnectionName))
        {
            (await byConnection.Database.SqlQueryRaw<string>("SELECT current_database() AS \"Value\"")
                .SingleAsync()).Should().Be(expectedDedicatedDatabase);
        }
    }

    [Fact]
    public async Task Customer_acme_external_database_has_manufacturing_schema()
    {
        var dedicatedConnection = ResolveDedicatedConnectionString();
        if (string.IsNullOrWhiteSpace(dedicatedConnection))
            throw SkipException.ForSkip("Set MANUFACTURING_DB_CUSTOMER_ACME_CONNECTION or TENANT_PLACEMENT_CONNECTIONS_FILE.");

        var options = new DbContextOptionsBuilder<ManufacturingDbContext>()
            .UseNpgsql(dedicatedConnection)
            .Options;

        await using var db = new ManufacturingDbContext(options);
        (await db.Database.CanConnectAsync()).Should().BeTrue();

        var tableExists = await db.Database.SqlQueryRaw<bool>(
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name = 'manufacturing_lots'
                ) AS "Value"
                """)
            .SingleAsync();
        tableExists.Should().BeTrue("external database should have Manufacturing EF schema (run migrate if missing)");
    }

    private static ITenantPlacementRegistry BuildRegistryFromFile()
    {
        var placementPath = ResolvePlacementFilePath();
        if (!File.Exists(placementPath))
            throw new InvalidOperationException($"Placement file not found: {placementPath}");

        var json = File.ReadAllText(placementPath);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var options = new TenantPlacementOptions
        {
            Enabled = true,
            DefaultTier = root.GetProperty("defaultTier").GetString() ?? TenantPlacementTier.Shared,
            ConfigPath = placementPath,
        };

        if (root.TryGetProperty("services", out var services))
        {
            foreach (var service in services.EnumerateObject())
            {
                options.Services[service.Name] = new TenantPlacementServiceOptions
                {
                    DefaultConnectionName = service.Value.GetProperty("defaultConnectionName").GetString()
                        ?? throw new InvalidOperationException($"Missing defaultConnectionName for {service.Name}"),
                };
            }
        }

        foreach (var placement in root.GetProperty("placements").EnumerateArray())
        {
            var entry = new TenantPlacementEntryOptions
            {
                TenantKey = placement.GetProperty("tenantKey").GetString() ?? "",
                Tier = placement.GetProperty("tier").GetString() ?? TenantPlacementTier.Shared,
                Active = placement.GetProperty("active").GetBoolean(),
                DataRegion = placement.TryGetProperty("dataRegion", out var region) ? region.GetString() : null,
                Reason = placement.TryGetProperty("reason", out var reason) ? reason.GetString() : null,
            };

            foreach (var service in placement.GetProperty("services").EnumerateObject())
            {
                entry.Services[service.Name] = new TenantPlacementServiceBindingOptions
                {
                    ConnectionName = service.Value.GetProperty("connectionName").GetString() ?? "",
                };
            }

            options.Placements.Add(entry);
        }

        var tenantEntry = options.Placements.FirstOrDefault(p =>
            string.Equals(p.TenantKey, TenantKey, StringComparison.OrdinalIgnoreCase));
        tenantEntry.Should().NotBeNull($"placement file must contain tenant '{TenantKey}'");
        tenantEntry!.Active.Should().BeTrue($"placement '{TenantKey}' must be active for routing test");

        return new TenantPlacementRegistry(
            Microsoft.Extensions.Options.Options.Create(options),
            new TestHostEnvironment { ContentRootPath = FindRepoRoot() },
            NullLogger<TenantPlacementRegistry>.Instance);
    }

    private static string ResolvePlacementFilePath()
    {
        var fromEnv = Environment.GetEnvironmentVariable("TENANT_PLACEMENT_FILE");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return Path.GetFullPath(fromEnv);

        return Path.Combine(FindRepoRoot(), "config", "conglomerate", "tenant-placement.v1.json");
    }

    private static string? ResolveSharedConnectionString()
    {
        foreach (var candidate in new[]
                 {
                     Environment.GetEnvironmentVariable("ConnectionStrings__ManufacturingDb"),
                     Environment.GetEnvironmentVariable("MANUFACTURING_TEST_POSTGRES_CONNECTION"),
                     Environment.GetEnvironmentVariable("DATABASE_MANUFACTURING_URL"),
                 })
        {
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate.Trim();
        }

        var fromFile = TryReadFromConnectionsFile(SharedConnectionName);
        return string.IsNullOrWhiteSpace(fromFile) ? null : fromFile;
    }

    private static string? ResolveDedicatedConnectionString()
    {
        var direct = Environment.GetEnvironmentVariable("MANUFACTURING_DB_CUSTOMER_ACME_CONNECTION")
            ?? Environment.GetEnvironmentVariable($"ConnectionStrings__{DedicatedConnectionName}");
        if (!string.IsNullOrWhiteSpace(direct))
            return direct.Trim();

        return TryReadFromConnectionsFile(DedicatedConnectionName);
    }

    private static string? TryReadFromConnectionsFile(string connectionName)
    {
        var file = Environment.GetEnvironmentVariable("TENANT_PLACEMENT_CONNECTIONS_FILE");
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
            return null;

        using var document = JsonDocument.Parse(File.ReadAllText(file));
        if (!document.RootElement.TryGetProperty(connectionName, out var value))
            return null;

        var text = value.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string GetDatabaseName(string connectionString)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
            throw new InvalidOperationException("Connection string is missing Database.");
        return builder.Database;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "His.Hope.sln")) ||
                Directory.Exists(Path.Combine(dir.FullName, "config", "conglomerate")))
                return dir.FullName;

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
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
