namespace His.Hope.Contracts.Commerce;

/// <summary>
/// Stable cross-service fact emitted after a commerce order is accepted.
/// The event is intentionally transport-neutral; Commerce persistence/outbox
/// is responsible for publishing it and Manufacturing consumes it idempotently.
/// </summary>
public sealed record CommerceOrderPlacedV1(
    Guid EventId,
    int SchemaVersion,
    DateTimeOffset OccurredAt,
    Guid OrderId,
    string TenantKey,
    string BuyerUserId,
    decimal TotalAmount,
    IReadOnlyList<CommerceOrderLineV1> Lines,
    string? CorrelationId = null,
    string? CausationId = null);

public sealed record CommerceOrderLineV1(
    string ProductId,
    string Sku,
    decimal Quantity,
    decimal UnitPrice);
