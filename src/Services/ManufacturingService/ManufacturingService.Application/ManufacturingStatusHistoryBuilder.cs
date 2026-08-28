using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application;

public static class ManufacturingStatusHistoryBuilder
{
    public static IReadOnlyList<EntityStatusHistoryDto> ForProductionBatch(
        Guid batchId,
        string tenantKey,
        string status,
        DateTimeOffset createdAt,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt)
    {
        var entries = new List<EntityStatusHistoryDto>
        {
            Entry(batchId, tenantKey, "production-batch", "", "Created", "system", createdAt),
        };

        if (startedAt.HasValue)
            entries.Add(Entry(batchId, tenantKey, "production-batch", "Created", "Started", "system", startedAt.Value));

        if (completedAt.HasValue)
            entries.Add(Entry(batchId, tenantKey, "production-batch", startedAt.HasValue ? "Started" : "Created", "Completed", "system", completedAt.Value));
        else if (status is "Cancelled")
            entries.Add(Entry(batchId, tenantKey, "production-batch", startedAt.HasValue ? "Started" : "Created", status, "system", startedAt ?? createdAt));

        return entries;
    }

    public static IReadOnlyList<EntityStatusHistoryDto> ForPurchaseOrder(
        Guid purchaseOrderId,
        string tenantKey,
        string status,
        DateTimeOffset orderedAt)
    {
        var entries = new List<EntityStatusHistoryDto>
        {
            Entry(purchaseOrderId, tenantKey, "purchase-order", "", "Draft", "system", orderedAt),
        };

        if (!status.Equals("Draft", StringComparison.OrdinalIgnoreCase))
            entries.Add(Entry(purchaseOrderId, tenantKey, "purchase-order", "Draft", status, "system", orderedAt));

        return entries;
    }

    public static IReadOnlyList<EntityStatusHistoryDto> ForDeviation(
        Guid deviationId,
        string tenantKey,
        string status,
        string requestedBy,
        DateTimeOffset createdAt,
        string? approvedBy,
        DateTimeOffset? approvedAt,
        DateTimeOffset? closedAt)
    {
        var entries = new List<EntityStatusHistoryDto>
        {
            Entry(deviationId, tenantKey, "deviation", "", "Requested", requestedBy, createdAt),
        };

        if (approvedAt.HasValue && approvedBy is not null)
            entries.Add(Entry(deviationId, tenantKey, "deviation", "Requested", status is "Rejected" ? "Rejected" : "Approved", approvedBy, approvedAt.Value));

        if (closedAt.HasValue)
            entries.Add(Entry(deviationId, tenantKey, "deviation", "Approved", "Closed", approvedBy ?? requestedBy, closedAt.Value));

        return entries;
    }

    private static EntityStatusHistoryDto Entry(
        Guid entityId,
        string tenantKey,
        string entityType,
        string fromStatus,
        string toStatus,
        string actor,
        DateTimeOffset occurredAt) =>
        new(Guid.NewGuid(), entityType, entityId, tenantKey, fromStatus, toStatus, actor, occurredAt);
}
