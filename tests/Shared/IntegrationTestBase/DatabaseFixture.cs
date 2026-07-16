using Testcontainers.PostgreSql;

namespace His.Hope.IntegrationTestBase;

/// <summary>
/// Manages the lifecycle of a PostgreSQL Testcontainer for integration tests.
/// Implement IAsyncLifetime in your test classes and call InitializeAsync/DisposeAsync.
/// </summary>
public class DatabaseFixture : IAsyncDisposable
{
    private readonly PostgreSqlContainer _container;
    private bool _disposed;

    public string ConnectionString => _container.GetConnectionString();
    public string Host => _container.Hostname;
    public int Port => _container.GetMappedPublicPort(5432);

    public DatabaseFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("hishopetest")
            .WithUsername("testuser")
            .WithPassword("testpass123!")
            .WithCleanUp(true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        // Optionally reset the database between tests by dropping and recreating schema
        // This is a placeholder - actual implementation depends on migration strategy
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            await _container.DisposeAsync();
        }
    }
}

/// <summary>
/// Collection definition to share the database fixture across multiple test classes.
/// </summary>
[CollectionDefinition("DatabaseIntegration")]
public class DatabaseIntegrationCollection : ICollectionFixture<DatabaseFixture>
{
}
