using FluentAssertions;
using His.Hope.CommerceService.Application.Orders;
using His.Hope.CommerceService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace CommerceService.Integration.Tests;

public sealed class CommerceCartPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("commercecart")
        .WithUsername("testuser")
        .WithPassword("testpass123!")
        .WithCleanUp(true)
        .Build();

    private PostgresCommerceCartPersistence persistence = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        var factory = new TestCommerceDbContextFactory(container.GetConnectionString());
        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        persistence = new PostgresCommerceCartPersistence(factory);
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    [Fact]
    public async Task Persists_cart_lines_and_replaces_previous_snapshot()
    {
        var first = new CommerceCartSnapshot(
            "tenant-a",
            "buyer-a",
            [new CommerceCartLineSnapshot(Guid.NewGuid(), 2), new CommerceCartLineSnapshot(Guid.NewGuid(), 4)]);
        await persistence.SaveCartAsync(first);

        var loaded = await persistence.GetCartAsync("tenant-a", "buyer-a");
        loaded.Lines.Should().BeEquivalentTo(first.Lines);

        var replacement = first with { Lines = [first.Lines[0] with { Quantity = 9 }] };
        await persistence.SaveCartAsync(replacement);

        var afterReplacement = await persistence.GetCartAsync("tenant-a", "buyer-a");
        afterReplacement.Lines.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(replacement.Lines[0]);
    }

    private sealed class TestCommerceDbContextFactory(string connectionString)
        : IDbContextFactory<CommerceDbContext>
    {
        public CommerceDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<CommerceDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            return new CommerceDbContext(options);
        }

        public Task<CommerceDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
