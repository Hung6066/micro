using FluentAssertions;
using His.Hope.CommerceService.Application.Orders;
using His.Hope.CommerceService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace CommerceService.Integration.Tests;

public sealed class CommerceRfqPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("commercerefquotes")
        .WithUsername("testuser")
        .WithPassword("testpass123!")
        .WithCleanUp(true)
        .Build();

    private PostgresCommerceRfqPersistence persistence = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        var factory = new TestCommerceDbContextFactory(container.GetConnectionString());
        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        persistence = new PostgresCommerceRfqPersistence(factory);
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    [Fact]
    public async Task Persists_quotes_with_lines_and_tenant_scope()
    {
        var id = Guid.NewGuid();
        var rfq = new CommerceRfqSnapshot(
            id, "tenant-a", "buyer-a", "submitted", "Need export pricing", null, null,
            DateTimeOffset.UtcNow, null,
            [new CommerceRfqLineSnapshot(Guid.NewGuid(), 12, "Private label")]);

        await persistence.SaveRfqAsync(rfq);
        (await persistence.GetRfqsAsync("tenant-a", "buyer-a"))
            .Should().ContainSingle().Which.Should().BeEquivalentTo(rfq, options => options.Excluding(x => x.CreatedAt));
        (await persistence.GetRfqsAsync("other-tenant", "buyer-a")).Should().BeEmpty();

        var updated = await persistence.UpdateRfqAsync(id, "tenant-a", "quoted", 1250m, "Available in 10 days");
        updated.Should().NotBeNull();
        updated!.Status.Should().Be("quoted");
        updated.QuotedTotal.Should().Be(1250m);
        updated.OperatorNotes.Should().Be("Available in 10 days");
    }

    private sealed class TestCommerceDbContextFactory(string connectionString) : IDbContextFactory<CommerceDbContext>
    {
        public CommerceDbContext CreateDbContext() => new(new DbContextOptionsBuilder<CommerceDbContext>().UseNpgsql(connectionString).Options);
        public Task<CommerceDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
