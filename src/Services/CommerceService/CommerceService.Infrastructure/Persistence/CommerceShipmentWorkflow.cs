using System.Text.Json;
using His.Hope.Contracts.Saga;
using His.Hope.ServiceDefaults;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.CommerceService.Infrastructure.Persistence;

public static class CommerceShipmentStates
{
    public const string Pending = "pending";
    public const string Created = "created";
    public const string Dispatched = "dispatched";
    public const string Delivered = "delivered";
    public const string Cancelled = "cancelled";
}

public sealed class CommerceShipmentEntity
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public string State { get; set; } = CommerceShipmentStates.Pending;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? ProviderShipmentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed record ShipmentDeliveryWebhook(string TenantKey, string ProviderShipmentId, DateTimeOffset DeliveredAt);

public sealed class CommerceShipmentWorkflow(IDbContextFactory<CommerceDbContext> dbFactory, IShipmentProvider provider)
{
    public async Task<CommerceShipmentEntity> CreateAsync(ShipmentRequestedV1 request, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var shipment = await db.Shipments.SingleOrDefaultAsync(x => x.TenantKey == request.TenantKey && x.OrderId == request.OrderId, ct);
        if (shipment is not null) return shipment;
        var result = await provider.CreateAsync(new ShipmentProviderRequest(request.OrderId, request.TenantKey, request.IdempotencyKey), ct);
        if (!result.Succeeded) throw new InvalidOperationException(result.FailureCode ?? "shipment_creation_failed");
        shipment = new CommerceShipmentEntity { Id = Guid.NewGuid(), OrderId = request.OrderId, TenantKey = request.TenantKey, State = CommerceShipmentStates.Created, IdempotencyKey = request.IdempotencyKey, ProviderShipmentId = result.ProviderShipmentId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Shipments.Add(shipment);
        AddOutbox(db, SagaMessagingContract.ShipmentCreated, request, result.ProviderShipmentId);
        await db.SaveChangesAsync(ct);
        return shipment;
    }

    public async Task DispatchAsync(ShipmentRequestedV1 request, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var shipment = await FindRequiredAsync(db, request, ct);
        if (shipment.State is CommerceShipmentStates.Dispatched or CommerceShipmentStates.Delivered) return;
        if (shipment.State != CommerceShipmentStates.Created) throw new InvalidOperationException("shipment_dispatch_requires_created");
        var result = await provider.DispatchAsync(new ShipmentProviderRequest(request.OrderId, request.TenantKey, request.IdempotencyKey, shipment.ProviderShipmentId), ct);
        if (!result.Succeeded) throw new InvalidOperationException(result.FailureCode ?? "shipment_dispatch_failed");
        shipment.State = CommerceShipmentStates.Dispatched;
        shipment.UpdatedAt = DateTime.UtcNow;
        AddOutbox(db, SagaMessagingContract.ShipmentDispatched, request, result.ProviderShipmentId);
        await db.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(ShipmentRequestedV1 request, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var shipment = await FindRequiredAsync(db, request, ct);
        if (shipment.State == CommerceShipmentStates.Cancelled) return;
        if (shipment.ProviderShipmentId is not null)
            await provider.CancelAsync(new ShipmentProviderRequest(request.OrderId, request.TenantKey, request.IdempotencyKey, shipment.ProviderShipmentId), ct);
        shipment.State = CommerceShipmentStates.Cancelled;
        shipment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> MarkDeliveredAsync(string tenantKey, string providerShipmentId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var shipment = await db.Shipments.SingleOrDefaultAsync(
            x => x.TenantKey == tenantKey && x.ProviderShipmentId == providerShipmentId, ct);
        if (shipment is null) return false;
        if (shipment.State == CommerceShipmentStates.Delivered) return true;
        if (shipment.State != CommerceShipmentStates.Dispatched)
            throw new InvalidOperationException("shipment_delivery_requires_dispatch");
        shipment.State = CommerceShipmentStates.Delivered;
        shipment.UpdatedAt = DateTime.UtcNow;
        db.OutboxMessages.Add(new CommerceOutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            Type = SagaMessagingContract.ShipmentDelivered,
            Content = JsonSerializer.Serialize(new ShipmentDeliveredV1(
                Guid.NewGuid(), SagaMessagingContract.CurrentSchemaVersion, DateTimeOffset.UtcNow,
                shipment.OrderId, shipment.TenantKey, shipment.ProviderShipmentId!, DateTimeOffset.UtcNow)),
            OccurredAt = DateTimeOffset.UtcNow,
            Status = His.Hope.Infrastructure.Outbox.OutboxStatus.Pending
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static async Task<CommerceShipmentEntity> FindRequiredAsync(CommerceDbContext db, ShipmentRequestedV1 request, CancellationToken ct) =>
        await db.Shipments.SingleOrDefaultAsync(x => x.TenantKey == request.TenantKey && x.OrderId == request.OrderId, ct)
        ?? throw new InvalidOperationException("commerce_shipment_not_found");

    private static void AddOutbox(CommerceDbContext db, string type, ShipmentRequestedV1 request, string providerShipmentId) =>
        db.OutboxMessages.Add(new CommerceOutboxMessageEntity { Id = Guid.NewGuid(), Type = type, Content = JsonSerializer.Serialize(request with { ShipmentId = providerShipmentId, EventId = Guid.NewGuid() }), OccurredAt = DateTimeOffset.UtcNow, Status = His.Hope.Infrastructure.Outbox.OutboxStatus.Pending });
}
