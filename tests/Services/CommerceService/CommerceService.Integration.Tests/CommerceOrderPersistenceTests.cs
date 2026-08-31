using FluentAssertions;
using His.Hope.CommerceService.Application.Orders;
using His.Hope.CommerceService.Infrastructure.Persistence;
using His.Hope.Contracts.Commerce;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace CommerceService.Integration.Tests;

public sealed class CommerceOrderPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("commerceintegration")
        .WithUsername("testuser")
        .WithPassword("testpass123!")
        .WithCleanUp(true)
        .Build();

    private CommerceDbContext db = null!;
    private PostgresCommerceOrderPersistence persistence = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;
        db = new CommerceDbContext(options);
        await db.Database.MigrateAsync();
        persistence = new PostgresCommerceOrderPersistence(
            new TestCommerceDbContextFactory(container.GetConnectionString()));
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        await container.DisposeAsync();
    }

    [Fact]
    public async Task Saves_order_and_outbox_atomically_and_deduplicates_replay()
    {
        var orderId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var order = new CommerceOrderSnapshot(
            orderId,
            "tenant-a",
            "buyer-a",
            "pending",
            25.50m,
            DateTimeOffset.UtcNow,
            [new CommerceOrderLineSnapshot(Guid.NewGuid(), "FG-MANGO", "Mango", 2, 12.75m)]);
        var @event = new CommerceOrderPlacedV1(
            eventId,
            1,
            order.CreatedAt,
            orderId,
            order.TenantKey,
            order.BuyerUserId,
            order.TotalAmount,
            [new CommerceOrderLineV1(order.Lines[0].ProductId.ToString(), "FG-MANGO", 2, 12.75m)]);

        await persistence.SaveOrderAndOutboxAsync(order, @event);
        await persistence.SaveOrderAndOutboxAsync(order, @event);

        (await db.Orders.CountAsync(x => x.Id == orderId)).Should().Be(1);
        (await db.OrderLines.CountAsync(x => x.OrderId == orderId)).Should().Be(1);
        (await db.OutboxMessages.CountAsync(x => x.Id == eventId)).Should().Be(1);
        (await db.OutboxMessages.SingleAsync(x => x.Id == eventId)).Type.Should().Be("Commerce.OrderPlaced.v1");

        var loaded = await persistence.GetOrderAsync(orderId, "tenant-a");
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be("pending");
        loaded.Lines.Should().ContainSingle(line => line.Sku == "FG-MANGO" && line.Quantity == 2);
        (await persistence.GetOrdersAsync("tenant-a", "buyer-a")).Should().ContainSingle(x => x.Id == orderId);

        var updated = await persistence.UpdateOrderStatusAsync(orderId, "tenant-a", "confirmed");
        updated.Should().NotBeNull();
        updated!.Status.Should().Be("confirmed");
    }

    [Fact]
    public void Order_status_policy_rejects_skipping_a_state()
    {
        CommerceOrderStatusPolicy.CanTransition("pending", "shipped").Should().BeFalse();
        CommerceOrderStatusPolicy.CanTransition("confirmed", "shipped").Should().BeTrue();
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
