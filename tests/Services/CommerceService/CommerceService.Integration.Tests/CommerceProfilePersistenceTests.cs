using FluentAssertions;
using His.Hope.CommerceService.Application.Orders;
using His.Hope.CommerceService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace CommerceService.Integration.Tests;

public sealed class CommerceProfilePersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("commerceprofile")
        .WithUsername("testuser")
        .WithPassword("testpass123!")
        .WithCleanUp(true)
        .Build();

    private PostgresCommerceProfilePersistence persistence = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        var factory = new TestCommerceDbContextFactory(container.GetConnectionString());
        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        persistence = new PostgresCommerceProfilePersistence(factory);
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    [Fact]
    public async Task Upserts_profile_and_preserves_tenant_user_boundary()
    {
        var profile = new CommerceProfileSnapshot(
            "tenant-a",
            "buyer-a",
            "Mai Buyer",
            "mai@example.com",
            "+84123456789",
            "Mai Foods",
            "wholesale");

        await persistence.SaveProfileAsync(profile);
        var loaded = await persistence.GetProfileAsync("tenant-a", "buyer-a", "fallback@example.com");
        loaded.Should().BeEquivalentTo(profile);

        var updated = profile with { CompanyName = "Mai Foods Export", PriceTier = "distributor" };
        await persistence.SaveProfileAsync(updated);
        (await persistence.GetProfileAsync("tenant-a", "buyer-a", "fallback@example.com"))
            .Should().BeEquivalentTo(updated);

        (await persistence.GetProfileAsync("other-tenant", "buyer-a", "fallback@example.com"))
            .TenantKey.Should().Be("other-tenant");
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
