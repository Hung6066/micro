using FluentAssertions;
using His.Hope.CommerceService.Api;
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
}
