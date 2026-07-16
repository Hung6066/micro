using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.IntegrationTestBase;

/// <summary>
/// Base class for integration tests that require a real database via Testcontainers
/// and a full application pipeline via WebApplicationFactory.
/// </summary>
/// <typeparam name="TProgram">The entry point class of the API under test (typically Program).</typeparam>
[Collection("DatabaseIntegration")]
public abstract class IntegrationTestBase<TProgram> : IAsyncLifetime
    where TProgram : class
{
    private readonly DatabaseFixture _databaseFixture;
    private WebApplicationFactory<TProgram>? _factory;
    private IServiceScope? _scope;
    private bool _initialized;

    protected HttpClient HttpClient { get; private set; } = null!;
    protected IServiceProvider ServiceProvider => _scope?.ServiceProvider ?? null!;

    protected IntegrationTestBase(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
        HttpClient = null!;
    }

    public virtual async Task InitializeAsync()
    {
        if (!_initialized)
        {
            await _databaseFixture.InitializeAsync();
            _initialized = true;
        }

        _factory = new WebApplicationFactory<TProgram>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Override the database connection to use Testcontainer
                    ReplaceDatabaseConnection(services);
                });
            });

        _scope = _factory.Services.CreateScope();
        HttpClient = _factory.CreateClient();
    }

    public virtual async Task DisposeAsync()
    {
        _scope?.Dispose();
        _factory?.Dispose();
        HttpClient?.Dispose();

        if (_initialized)
        {
            await _databaseFixture.DisposeAsync();
            _initialized = false;
        }
    }

    /// <summary>
    /// Override this method to replace the EF Core DbContext or database connection
    /// with the Testcontainer connection string.
    /// </summary>
    protected virtual void ReplaceDatabaseConnection(IServiceCollection services)
    {
        // Example: Remove existing DbContext registration and add one using Testcontainer
        // var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        // if (descriptor != null) services.Remove(descriptor);
        // services.AddDbContext<AppDbContext>(options =>
        //     options.UseNpgsql(_databaseFixture.ConnectionString));
    }

    /// <summary>
    /// Resets the database state between tests by clearing all data.
    /// Override to provide custom cleanup logic.
    /// </summary>
    protected virtual Task ResetDatabaseAsync()
    {
        return _databaseFixture.ResetDatabaseAsync();
    }

    /// <summary>
    /// Helper to get a scoped service from the test application's DI container.
    /// </summary>
    protected TService GetService<TService>() where TService : notnull
    {
        return ServiceProvider.GetRequiredService<TService>();
    }
}
