using His.Hope.CommerceService.Application.Orders;
using His.Hope.Contracts.Commerce;

namespace His.Hope.CommerceService.Infrastructure.Persistence;

// Development/test-only adapters keep protocol and authorization tests independent
// from PostgreSQL. Production registration rejects a missing CommerceDb instead.
internal sealed class InMemoryCommerceOrderPersistence : ICommerceOrderPersistence
{
    public Task SaveOrderAndOutboxAsync(CommerceOrderSnapshot order, CommerceOrderPlacedV1 @event, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<CommerceOrderView>> GetOrdersAsync(string tenantKey, string? buyerUserId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CommerceOrderView>>([]);

    public Task<CommerceOrderView?> GetOrderAsync(Guid orderId, string tenantKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<CommerceOrderView?>(null);

    public Task<CommerceOrderView?> UpdateOrderStatusAsync(Guid orderId, string tenantKey, string status, CancellationToken cancellationToken = default) =>
        Task.FromResult<CommerceOrderView?>(null);
}

internal sealed class InMemoryCommerceCatalogPersistence : ICommerceCatalogPersistence
{
    public Task<IReadOnlyList<CommerceProductSnapshot>> GetProductsAsync(string tenantKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CommerceProductSnapshot>>([]);

    public Task SaveProductsAsync(IReadOnlyList<CommerceProductSnapshot> products, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class InMemoryCommerceCartPersistence : ICommerceCartPersistence
{
    public Task<CommerceCartSnapshot> GetCartAsync(string tenantKey, string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CommerceCartSnapshot(tenantKey, userId, []));

    public Task SaveCartAsync(CommerceCartSnapshot cart, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class InMemoryCommerceProfilePersistence : ICommerceProfilePersistence
{
    public Task<CommerceProfileSnapshot> GetProfileAsync(string tenantKey, string userId, string fallbackEmail, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CommerceProfileSnapshot(tenantKey, userId, fallbackEmail.Split('@')[0], fallbackEmail, "", "", "standard"));

    public Task SaveProfileAsync(CommerceProfileSnapshot profile, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class InMemoryCommerceNotificationPersistence : ICommerceNotificationPersistence
{
    public Task<IReadOnlyList<CommerceNotificationSnapshot>> GetNotificationsAsync(string tenantKey, string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CommerceNotificationSnapshot>>([]);

    public Task SaveNotificationAsync(CommerceNotificationSnapshot notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task MarkAsReadAsync(Guid id, string tenantKey, string userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class InMemoryCommerceRfqPersistence : ICommerceRfqPersistence
{
    public Task SaveRfqAsync(CommerceRfqSnapshot rfq, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<CommerceRfqSnapshot>> GetRfqsAsync(string tenantKey, string? buyerUserId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CommerceRfqSnapshot>>([]);

    public Task<CommerceRfqSnapshot?> GetRfqAsync(Guid id, string tenantKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<CommerceRfqSnapshot?>(null);

    public Task<CommerceRfqSnapshot?> UpdateRfqAsync(Guid id, string tenantKey, string status, decimal quotedTotal, string operatorNotes, CancellationToken cancellationToken = default) =>
        Task.FromResult<CommerceRfqSnapshot?>(null);
}
