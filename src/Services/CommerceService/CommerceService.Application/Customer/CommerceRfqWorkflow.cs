using His.Hope.CommerceService.Application.Orders;

namespace His.Hope.CommerceService.Application.Customer;

public interface ICommerceRfqWorkflow
{
    Task<CommerceRfqSnapshot?> CreateAsync(
        string tenantKey,
        string buyerUserId,
        string message,
        IReadOnlyList<CommerceRfqLineSnapshot> lines,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommerceRfqSnapshot>> GetManyAsync(
        string tenantKey,
        string? buyerUserId = null,
        CancellationToken cancellationToken = default);

    Task<CommerceRfqSnapshot?> GetAsync(Guid id, string tenantKey, CancellationToken cancellationToken = default);

    Task<CommerceRfqSnapshot?> RespondAsync(
        Guid id,
        string tenantKey,
        string status,
        decimal quotedTotal,
        string operatorNotes,
        CancellationToken cancellationToken = default);
}

public sealed class CommerceRfqWorkflow(
    ICommerceRfqPersistence rfqPersistence,
    ICommerceCatalogPersistence catalogPersistence) : ICommerceRfqWorkflow
{
    public async Task<CommerceRfqSnapshot?> CreateAsync(
        string tenantKey,
        string buyerUserId,
        string message,
        IReadOnlyList<CommerceRfqLineSnapshot> lines,
        CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
            return null;

        var products = await catalogPersistence.GetProductsAsync(tenantKey, cancellationToken);
        var productIds = products.Select(product => product.Id).ToHashSet();
        var sanitized = lines
            .Where(line => line.Quantity > 0 && productIds.Contains(line.ProductId))
            .Select(line => line with { Quantity = Math.Min(line.Quantity, 999999) })
            .ToArray();
        if (sanitized.Length == 0)
            return null;

        var rfq = new CommerceRfqSnapshot(
            Guid.NewGuid(),
            tenantKey,
            buyerUserId,
            "submitted",
            message.Trim(),
            null,
            null,
            DateTimeOffset.UtcNow,
            null,
            sanitized);
        await rfqPersistence.SaveRfqAsync(rfq, cancellationToken);
        return rfq;
    }

    public Task<IReadOnlyList<CommerceRfqSnapshot>> GetManyAsync(
        string tenantKey,
        string? buyerUserId = null,
        CancellationToken cancellationToken = default) =>
        rfqPersistence.GetRfqsAsync(tenantKey, buyerUserId, cancellationToken);

    public Task<CommerceRfqSnapshot?> GetAsync(Guid id, string tenantKey, CancellationToken cancellationToken = default) =>
        rfqPersistence.GetRfqAsync(id, tenantKey, cancellationToken);

    public Task<CommerceRfqSnapshot?> RespondAsync(
        Guid id,
        string tenantKey,
        string status,
        decimal quotedTotal,
        string operatorNotes,
        CancellationToken cancellationToken = default) =>
        rfqPersistence.UpdateRfqAsync(id, tenantKey, status, quotedTotal, operatorNotes, cancellationToken);
}
