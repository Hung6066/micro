using Testcontainers.PostgreSql;
using Npgsql;

namespace His.Hope.IntegrationTestBase;

/// <summary>
/// Manages the lifecycle of a PostgreSQL Testcontainer for integration tests.
/// xUnit owns the fixture lifecycle through IAsyncLifetime.
/// </summary>
public class DatabaseFixture : IAsyncLifetime, IAsyncDisposable
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
        await WaitForHostPortAsync(_container.GetConnectionString());
    }

    private static async Task WaitForHostPortAsync(string connectionString)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                await connection.CloseAsync();
                return;
            }
            catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
            {
                last = ex;
                await Task.Delay(250);
            }
        }

        throw new TimeoutException("PostgreSQL Testcontainer was ready internally but its mapped host port was not reachable.", last);
    }

    public async Task ResetDatabaseAsync()
    {
        // Optionally reset the database between tests by dropping and recreating schema
        // This is a placeholder - actual implementation depends on migration strategy
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            await _container.DisposeAsync();
        }
    }

    ValueTask IAsyncDisposable.DisposeAsync() => new(DisposeAsync());
}

/// <summary>
/// Collection definition to share the database fixture across multiple test classes.
/// </summary>
[CollectionDefinition("DatabaseIntegration")]
public class DatabaseIntegrationCollection : ICollectionFixture<DatabaseFixture>
{
}
