using Microsoft.EntityFrameworkCore;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.ManufacturingService.Application;
using System.Text.Json;

public sealed partial class PostgresManufacturingStore : IManufacturingQualityWorkflowStore
{
    public async Task<(ManufacturingDeviationDto? Deviation, string? Error)> CreateDeviationAsync(
        Guid productionBatchId, string tenantKey, CreateDeviationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Type) || string.IsNullOrWhiteSpace(request.Description) ||
            string.IsNullOrWhiteSpace(request.Impact) || string.IsNullOrWhiteSpace(request.RequestedBy))
            return (null, "invalid_deviation");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var batch = await db.ProductionBatches.SingleOrDefaultAsync(x => x.Id == productionBatchId, cancellationToken);
        if (batch is null) return (null, ManufacturingErrorCodes.ProductionBatchNotFound);
        if (!batch.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantScopeDenied);
        if (batch.Status is ManufacturingStatusCodes.Completed or ManufacturingStatusCodes.Cancelled or ManufacturingStatusCodes.Closed) return (null, "batch_not_active");

        var now = DateTimeOffset.UtcNow;
        var entity = new ManufacturingDeviationEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, ProductionBatchId = productionBatchId,
            Type = request.Type.Trim(), Description = request.Description.Trim(), Impact = request.Impact.Trim(),
            Status = "Requested", RequestedBy = request.RequestedBy.Trim(), CreatedAt = now
        };
        db.Deviations.Add(entity);
        EntityStatusHistoryStore.Append(db, "deviation", entity.Id, tenantKey, "", "Requested", entity.RequestedBy, now);
        AddDeviationEvent(db, entity, "Raised", entity.RequestedBy, now);
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(entity), null);
    }

    public async Task<IReadOnlyList<ManufacturingDeviationDto>> GetDeviationsAsync(
        string tenantKey, Guid? productionBatchId, string? status, int limit, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Deviations.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (productionBatchId.HasValue) query = query.Where(x => x.ProductionBatchId == productionBatchId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return (await query.OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(limit, 1, 200)).ToListAsync(cancellationToken)).Select(ToDto).ToList();
    }

    public async Task<(ManufacturingDeviationDto? Deviation, string? Error)> ChangeDeviationStatusAsync(
        Guid deviationId, string tenantKey, string targetStatus, DeviationActionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Actor)) return (null, "invalid_deviation_actor");
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Deviations.SingleOrDefaultAsync(x => x.Id == deviationId, cancellationToken);
        if (entity is null) return (null, ManufacturingErrorCodes.DeviationNotFound);
        if (!entity.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantScopeDenied);

        var actor = request.Actor.Trim();
        var valid = (entity.Status, targetStatus) switch
        {
            ("Requested", ManufacturingStatusCodes.Approved) => !entity.RequestedBy.Equals(actor, StringComparison.OrdinalIgnoreCase),
            ("Requested", ManufacturingStatusCodes.Rejected) => !entity.RequestedBy.Equals(actor, StringComparison.OrdinalIgnoreCase),
            (ManufacturingStatusCodes.Approved, ManufacturingStatusCodes.Closed) => true,
            _ => false
        };
        if (!valid)
            return (null, targetStatus is ManufacturingStatusCodes.Approved or ManufacturingStatusCodes.Rejected && entity.RequestedBy.Equals(actor, StringComparison.OrdinalIgnoreCase)
                ? "author_cannot_approve_own_deviation"
                : "invalid_deviation_transition");

        var now = DateTimeOffset.UtcNow;
        var previousStatus = entity.Status;
        entity.Status = targetStatus;
        entity.ResolutionNotes = request.Notes?.Trim();
        if (targetStatus is ManufacturingStatusCodes.Approved or ManufacturingStatusCodes.Rejected)
        {
            entity.ApprovedBy = actor;
            entity.ApprovedAt = now;
        }
        if (targetStatus == ManufacturingStatusCodes.Closed) entity.ClosedAt = now;
        EntityStatusHistoryStore.Append(db, "deviation", entity.Id, tenantKey, previousStatus, targetStatus, actor, now);
        AddDeviationEvent(db, entity, targetStatus, actor, now);
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(entity), null);
    }

    public async Task<IReadOnlyList<EntityStatusHistoryDto>> GetDeviationStatusHistoryAsync(string tenantKey, Guid deviationId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Deviations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == deviationId, cancellationToken);
        if (entity is null || !entity.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase))
            return [];

        var persisted = await EntityStatusHistoryStore.GetAsync(db, tenantKey, "deviation", deviationId, cancellationToken);
        if (persisted.Count > 0)
            return persisted;

        return ManufacturingStatusHistoryBuilder.ForDeviation(
            entity.Id,
            entity.TenantKey,
            entity.Status,
            entity.RequestedBy,
            entity.CreatedAt,
            entity.ApprovedBy,
            entity.ApprovedAt,
            entity.ClosedAt);
    }

    private static void AddDeviationEvent(ManufacturingDbContext db, ManufacturingDeviationEntity entity, string action, string actor, DateTimeOffset occurredAt)
    {
        var eventId = Guid.NewGuid();
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = eventId,
            Type = $"Manufacturing.Deviation{action}.v1",
            Content = JsonSerializer.Serialize(new
            {
                eventId, schemaVersion = 1, occurredAt, correlationId = entity.Id, deviationId = entity.Id,
                productionBatchId = entity.ProductionBatchId, tenantKey = entity.TenantKey, status = entity.Status,
                actor, type = entity.Type, impact = entity.Impact
            }),
            OccurredOn = occurredAt.UtcDateTime,
            Status = ManufacturingStatusCodes.Pending
        });
    }

    private static ManufacturingDeviationDto ToDto(ManufacturingDeviationEntity x) => new(
        x.Id, x.TenantKey, x.ProductionBatchId, x.Type, x.Description, x.Impact, x.Status, x.RequestedBy,
        x.ApprovedBy, x.ResolutionNotes, x.CreatedAt, x.ApprovedAt, x.ClosedAt);
}

public sealed class ManufacturingDeviationEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public Guid ProductionBatchId { get; set; }
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public string Impact { get; set; } = "";
    public string Status { get; set; } = "Requested";
    public string RequestedBy { get; set; } = "";
    public string? ApprovedBy { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}
