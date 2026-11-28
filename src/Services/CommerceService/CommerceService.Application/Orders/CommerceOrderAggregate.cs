namespace His.Hope.CommerceService.Application.Orders;

public sealed class CommerceOrderAggregate
{
    private CommerceOrderAggregate(CommerceOrderSnapshot snapshot) => Snapshot = snapshot;

    public CommerceOrderSnapshot Snapshot { get; }

    public static CommerceOrderAggregate? Create(
        string tenantKey,
        string buyerUserId,
        CommerceCartSnapshot cart,
        CommerceProfileSnapshot profile,
        IReadOnlyList<CommerceProductSnapshot> products,
        DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(tenantKey) ||
            string.IsNullOrWhiteSpace(buyerUserId) ||
            cart.Lines.Count == 0)
            return null;

        var productsById = products
            .Where(product => string.Equals(product.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(product => product.Id);
        var lines = cart.Lines
            .Where(line => line.Quantity > 0 && productsById.ContainsKey(line.ProductId))
            .Select(line =>
            {
                var product = productsById[line.ProductId];
                var unitPrice = ResolveUnitPrice(product, profile.PriceTier);
                return new CommerceOrderLineSnapshot(
                    product.Id,
                    product.Sku,
                    product.Name,
                    line.Quantity,
                    unitPrice);
            })
            .ToArray();

        if (lines.Length == 0)
            return null;

        var snapshot = new CommerceOrderSnapshot(
            Guid.NewGuid(),
            tenantKey,
            buyerUserId,
            "pending",
            lines.Sum(line => line.UnitPrice * line.Quantity),
            createdAt ?? DateTimeOffset.UtcNow,
            lines);

        return new CommerceOrderAggregate(snapshot);
    }

    public bool CanTransitionTo(string requestedStatus) =>
        CommerceOrderStatusPolicy.CanTransition(Snapshot.Status, requestedStatus);

    private static decimal ResolveUnitPrice(CommerceProductSnapshot product, string? priceTier) =>
        priceTier?.Trim().ToLowerInvariant() switch
        {
            "distributor" => product.WholesaleUnitPrice * 0.92m,
            "wholesale" => product.WholesaleUnitPrice,
            _ => product.UnitPrice,
        };
}
