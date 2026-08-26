using FluentAssertions;
using His.Hope.Contracts.Commerce;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class CommerceOrderAllocationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("manufacturingcommerce")
        .WithUsername("testuser")
        .WithPassword("testpass123!")
        .WithCleanUp(true)
        .Build();

    private ManufacturingDbContext db = null!;
    private ManufacturingReservationStore store = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        var factory = new TestDbContextFactory(container.GetConnectionString());
        db = factory.CreateDbContext();
        await db.Database.MigrateAsync();
        db.Lots.Add(new ManufacturingLotEntity
        {
            Id = Guid.NewGuid(),
            TenantKey = "tenant-a",
            Sku = "FG-MANGO",
            Quantity = 50,
            Uom = "kg",
            Disposition = "Released",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        store = new ManufacturingReservationStore(factory);
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        await container.DisposeAsync();
    }

    [Fact]
    public async Task Allocates_order_and_deduplicates_same_order_replay()
    {
        var orderId = Guid.NewGuid();
        var order = new CommerceOrderPlacedV1(
            Guid.NewGuid(),
            1,
            DateTimeOffset.UtcNow,
            orderId,
            "tenant-a",
            "buyer-a",
            100,
            [new CommerceOrderLineV1(Guid.NewGuid().ToString(), "FG-MANGO", 20, 5)]);

        var first = store.AllocateCommerceOrder(order);
        var replay = store.AllocateCommerceOrder(order with { EventId = Guid.NewGuid() });

        first.Error.Should().BeNull();
        first.Allocations.Should().ContainSingle();
        replay.Error.Should().BeNull();
        replay.Allocations.Should().BeEmpty();
        (await db.LotReservations.CountAsync(x => x.ReferenceId == orderId)).Should().Be(1);
        (await db.EventReceipts.CountAsync(x => x.EventType == "Commerce.OrderPlaced.v1")).Should().Be(1);
    }

    private sealed class TestDbContextFactory(string connectionString)
        : IDbContextFactory<ManufacturingDbContext>
    {
        public ManufacturingDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ManufacturingDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            return new ManufacturingDbContext(options);
        }

        public Task<ManufacturingDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
