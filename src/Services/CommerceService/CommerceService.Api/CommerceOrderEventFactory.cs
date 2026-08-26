using His.Hope.Contracts.Commerce;

namespace His.Hope.CommerceService.Api;

public static class CommerceOrderEventFactory
{
    public static CommerceOrderPlacedV1 Create(OrderDto order, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(order);

        return new CommerceOrderPlacedV1(
            EventId: Guid.NewGuid(),
            SchemaVersion: 1,
            OccurredAt: order.CreatedAt,
            OrderId: order.Id,
            TenantKey: order.TenantKey,
            BuyerUserId: order.BuyerUserId,
            TotalAmount: order.TotalAmount,
            Lines: order.Lines
                .Select(line => new CommerceOrderLineV1(
                    line.ProductId.ToString(),
                    line.Sku,
                    line.Quantity,
                    line.UnitPrice))
                .ToArray(),
            CorrelationId: correlationId);
    }
}
