using FluentAssertions;
using His.Hope.CommerceService.Application.Orders;
using His.Hope.CommerceService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace CommerceService.Integration.Tests;

public sealed class CommerceCatalogPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("commercecatalog")
        .WithUsername("testuser")
        .WithPassword("testpass123!")
        .WithCleanUp(true)
        .Build();

    private PostgresCommerceCatalogPersistence persistence = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        var factory = new TestCommerceDbContextFactory(container.GetConnectionString());
        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        persistence = new PostgresCommerceCatalogPersistence(factory);
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    [Fact]
    public async Task Persists_catalog_by_tenant_and_updates_product()
    {
        var product = new CommerceProductSnapshot(Guid.NewGuid(), "tenant-a", "SKU-1", "Mango", "Dried mango", 85m, 72m, 10, true, true);
        await persistence.SaveProductsAsync([product]);

        (await persistence.GetProductsAsync("tenant-a")).Should().ContainSingle().Which.Should().BeEquivalentTo(product);
        (await persistence.GetProductsAsync("tenant-b")).Should().BeEmpty();

        var changed = product with { Name = "Mango premium", UnitPrice = 90m };
        await persistence.SaveProductsAsync([changed]);
        (await persistence.GetProductsAsync("tenant-a")).Single().Should().BeEquivalentTo(changed);
    }

    private sealed class TestCommerceDbContextFactory(string connectionString) : IDbContextFactory<CommerceDbContext>
    {
        public CommerceDbContext CreateDbContext() => new(new DbContextOptionsBuilder<CommerceDbContext>().UseNpgsql(connectionString).Options);
        public Task<CommerceDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
