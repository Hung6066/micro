using FluentAssertions;
using His.Hope.CommerceService.Application.Orders;
using His.Hope.CommerceService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace CommerceService.Integration.Tests;

public sealed class CommerceNotificationPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("commercenotifications")
        .WithUsername("testuser")
        .WithPassword("testpass123!")
        .WithCleanUp(true)
        .Build();

    private PostgresCommerceNotificationPersistence persistence = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        var factory = new TestCommerceDbContextFactory(container.GetConnectionString());
        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        persistence = new PostgresCommerceNotificationPersistence(factory);
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    [Fact]
    public async Task Persists_notifications_and_keeps_tenant_user_scope()
    {
        var notification = new CommerceNotificationSnapshot(
            Guid.NewGuid(),
            "tenant-a",
            "buyer-a",
            "Order placed",
            "Order submitted",
            DateTimeOffset.UtcNow,
            false);

        await persistence.SaveNotificationAsync(notification);
        (await persistence.GetNotificationsAsync("tenant-a", "buyer-a"))
            .Should().ContainSingle().Which.Should().BeEquivalentTo(notification, options => options.Excluding(x => x.CreatedAt));
        (await persistence.GetNotificationsAsync("other-tenant", "buyer-a"))
            .Should().BeEmpty();

        await persistence.MarkAsReadAsync(notification.Id, "tenant-a", "buyer-a");
        (await persistence.GetNotificationsAsync("tenant-a", "buyer-a"))
            .Single().IsRead.Should().BeTrue();
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
