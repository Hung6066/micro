using FluentAssertions;
using His.Hope.CommerceService.Application.Customer;
using His.Hope.CommerceService.Application.Orders;
using Xunit;

namespace CommerceService.Integration.Tests;

public sealed class CommerceWorkflowTests
{
    [Theory]
    [InlineData("submitted", "quoted", true)]
    [InlineData("quoted", "declined", false)]
    [InlineData("declined", "closed", true)]
    [InlineData("closed", "quoted", false)]
    public void Rfq_status_policy_prevents_invalid_transitions(string current, string requested, bool expected)
    {
        CommerceRfqStatusPolicy.CanTransition(current, requested).Should().Be(expected);
    }

    [Fact]
    public async Task Rfq_workflow_scopes_products_to_the_requested_tenant_and_sanitizes_lines()
    {
        var productId = Guid.NewGuid();
        var catalog = new FakeCatalogPersistence([new CommerceProductSnapshot(productId, "tenant-a", "SKU-A", "A", "", 10, 8, 1, true, true)]);
        var persistence = new FakeRfqPersistence();
        var workflow = new CommerceRfqWorkflow(persistence, catalog);

        var rfq = await workflow.CreateAsync("tenant-a", "buyer-a", "  request  ", [
            new CommerceRfqLineSnapshot(productId, 1_000_000, "  note  "),
            new CommerceRfqLineSnapshot(Guid.NewGuid(), 2, null)]);

        rfq.Should().NotBeNull();
        rfq!.Message.Should().Be("request");
        rfq.Lines.Should().ContainSingle().Which.Quantity.Should().Be(999999);
        persistence.Saved.Should().BeEquivalentTo(rfq);
    }

    [Fact]
    public async Task Rfq_workflow_rejects_a_request_without_products_in_the_tenant_catalog()
    {
        var workflow = new CommerceRfqWorkflow(new FakeRfqPersistence(), new FakeCatalogPersistence([]));

        var rfq = await workflow.CreateAsync("tenant-a", "buyer-a", "request", [new CommerceRfqLineSnapshot(Guid.NewGuid(), 1, null)]);

        rfq.Should().BeNull();
    }

    private sealed class FakeCatalogPersistence(IReadOnlyList<CommerceProductSnapshot> products) : ICommerceCatalogPersistence
    {
        public IReadOnlyList<CommerceProductSnapshot> Products { get; } = products;
        public Task<IReadOnlyList<CommerceProductSnapshot>> GetProductsAsync(string tenantKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CommerceProductSnapshot>>(Products.Where(product => product.TenantKey == tenantKey).ToArray());
        public Task SaveProductsAsync(IReadOnlyList<CommerceProductSnapshot> products, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeRfqPersistence : ICommerceRfqPersistence
    {
        public CommerceRfqSnapshot? Saved { get; private set; }
        public Task SaveRfqAsync(CommerceRfqSnapshot rfq, CancellationToken cancellationToken = default) { Saved = rfq; return Task.CompletedTask; }
        public Task<IReadOnlyList<CommerceRfqSnapshot>> GetRfqsAsync(string tenantKey, string? buyerUserId = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CommerceRfqSnapshot>>([]);
        public Task<CommerceRfqSnapshot?> GetRfqAsync(Guid id, string tenantKey, CancellationToken cancellationToken = default) => Task.FromResult<CommerceRfqSnapshot?>(null);
        public Task<CommerceRfqSnapshot?> UpdateRfqAsync(Guid id, string tenantKey, string status, decimal quotedTotal, string operatorNotes, CancellationToken cancellationToken = default) => Task.FromResult<CommerceRfqSnapshot?>(null);
    }
}
