using His.Hope.Contracts.Commerce;

namespace His.Hope.CommerceService.Application.Orders;

public sealed record CommerceOrderSnapshot(
    Guid Id,
    string TenantKey,
    string BuyerUserId,
    string Status,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    IReadOnlyList<CommerceOrderLineSnapshot> Lines);

public sealed record CommerceOrderLineSnapshot(
    Guid ProductId,
    string Sku,
    string Name,
    int Quantity,
    decimal UnitPrice);

public sealed record CommerceProductSnapshot(
    Guid Id,
    string TenantKey,
    string Sku,
    string Name,
    string Description,
    decimal UnitPrice,
    decimal WholesaleUnitPrice,
    int MinOrderQty,
    bool SupportsPrivateLabel,
    bool SupportsExport);

public sealed record CommerceCartSnapshot(
    string TenantKey,
    string UserId,
    IReadOnlyList<CommerceCartLineSnapshot> Lines);

public sealed record CommerceCartLineSnapshot(Guid ProductId, int Quantity);

public sealed record CommerceProfileSnapshot(
    string TenantKey,
    string UserId,
    string DisplayName,
    string Email,
    string Phone,
    string CompanyName,
    string PriceTier);

public sealed record CommerceNotificationSnapshot(
    Guid Id,
    string TenantKey,
    string UserId,
    string Title,
    string Message,
    DateTimeOffset CreatedAt,
    bool IsRead);

public sealed record CommerceRfqSnapshot(
    Guid Id,
    string TenantKey,
    string BuyerUserId,
    string Status,
    string Message,
    decimal? QuotedTotal,
    string? OperatorNotes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt,
    IReadOnlyList<CommerceRfqLineSnapshot> Lines);

public sealed record CommerceRfqLineSnapshot(Guid ProductId, int Quantity, string? Notes);

public sealed record CommerceOrderView(
    Guid Id,
    string TenantKey,
    string BuyerUserId,
    string Status,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    IReadOnlyList<CommerceOrderLineSnapshot> Lines);

public interface ICommerceOrderPersistence
{
    Task SaveOrderAndOutboxAsync(
        CommerceOrderSnapshot order,
        CommerceOrderPlacedV1 @event,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommerceOrderView>> GetOrdersAsync(
        string tenantKey,
        string? buyerUserId = null,
        CancellationToken cancellationToken = default);

    Task<CommerceOrderView?> GetOrderAsync(
        Guid orderId,
        string tenantKey,
        CancellationToken cancellationToken = default);

    Task<CommerceOrderView?> UpdateOrderStatusAsync(
        Guid orderId,
        string tenantKey,
        string status,
        CancellationToken cancellationToken = default);
}

public interface ICommerceCatalogPersistence
{
    Task<IReadOnlyList<CommerceProductSnapshot>> GetProductsAsync(string tenantKey, CancellationToken cancellationToken = default);
    Task SaveProductsAsync(IReadOnlyList<CommerceProductSnapshot> products, CancellationToken cancellationToken = default);
}

public interface ICommerceCartPersistence
{
    Task<CommerceCartSnapshot> GetCartAsync(
        string tenantKey,
        string userId,
        CancellationToken cancellationToken = default);

    Task SaveCartAsync(
        CommerceCartSnapshot cart,
        CancellationToken cancellationToken = default);
}

public interface ICommerceProfilePersistence
{
    Task<CommerceProfileSnapshot> GetProfileAsync(
        string tenantKey,
        string userId,
        string fallbackEmail,
        CancellationToken cancellationToken = default);

    Task SaveProfileAsync(
        CommerceProfileSnapshot profile,
        CancellationToken cancellationToken = default);
}

public interface ICommerceNotificationPersistence
{
    Task<IReadOnlyList<CommerceNotificationSnapshot>> GetNotificationsAsync(string tenantKey, string userId, CancellationToken cancellationToken = default);
    Task SaveNotificationAsync(CommerceNotificationSnapshot notification, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(Guid id, string tenantKey, string userId, CancellationToken cancellationToken = default);
}

public interface ICommerceRfqPersistence
{
    Task SaveRfqAsync(CommerceRfqSnapshot rfq, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CommerceRfqSnapshot>> GetRfqsAsync(string tenantKey, string? buyerUserId = null, CancellationToken cancellationToken = default);
    Task<CommerceRfqSnapshot?> GetRfqAsync(Guid id, string tenantKey, CancellationToken cancellationToken = default);
    Task<CommerceRfqSnapshot?> UpdateRfqAsync(Guid id, string tenantKey, string status, decimal quotedTotal, string operatorNotes, CancellationToken cancellationToken = default);
}
