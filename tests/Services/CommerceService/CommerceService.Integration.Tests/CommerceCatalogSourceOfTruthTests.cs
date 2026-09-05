using FluentAssertions;
using His.Hope.CommerceService.Api;
using His.Hope.CommerceService.Application.Orders;
using Xunit;

namespace CommerceService.Integration.Tests;

public sealed class CommerceCatalogSourceOfTruthTests
{
    [Fact]
    public void Uses_persisted_tenant_catalog_for_listing_and_order_pricing()
    {
        var store = new CommerceStore();
        var seed = store.GetSeedProducts().First();
        var persisted = seed with
        {
            TenantKey = "tenant-b",
            Name = "Tenant B mango",
            UnitPrice = 123m,
            WholesaleUnitPrice = 99m,
        };

        var catalog = store.GetProductsForBuyer("tenant-b", "standard", [persisted]);
        catalog.Should().ContainSingle().Which.Name.Should().Be("Tenant B mango");
        var detail = store.GetProductForBuyer("tenant-b", "standard", persisted.Id, [persisted]);
        detail.Should().NotBeNull();
        detail!.Name.Should().Be("Tenant B mango");
        store.GetProductForBuyer("tenant-a", "standard", persisted.Id, [persisted]).Should().BeNull();

        var order = store.CreateOrder(
            "tenant-b",
            "buyer-b",
            "buyer-b@example.test",
            new CartDto("tenant-b", [new CartLineDto(persisted.Id, 2)]),
            "standard",
            [persisted]);

        order.Should().NotBeNull();
        order!.Lines.Should().ContainSingle().Which.UnitPrice.Should().Be(123m);
        order.TotalAmount.Should().Be(246m);
    }

    [Fact]
    public void Order_aggregate_rejects_products_from_another_tenant_and_snapshots_price()
    {
        var product = new CommerceProductSnapshot(
            Guid.NewGuid(), "tenant-a", "SKU-1", "Mango", "Dried mango", 123m, 99m, 1, true, true);
        var aggregate = CommerceOrderAggregate.Create(
            "tenant-a",
            "buyer-a",
            new CommerceCartSnapshot("tenant-a", "buyer-a", [new CommerceCartLineSnapshot(product.Id, 2)]),
            new CommerceProfileSnapshot("tenant-a", "buyer-a", "Buyer", "buyer@example.test", "", "", "wholesale"),
            [product]);

        aggregate.Should().NotBeNull();
        aggregate!.Snapshot.TotalAmount.Should().Be(198m);
        aggregate.Snapshot.Status.Should().Be("pending");
        aggregate.CanTransitionTo("shipped").Should().BeFalse();
    }
}
