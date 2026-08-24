using Microsoft.EntityFrameworkCore;
using His.Hope.ManufacturingService.Application.Ports;
using System.Text.Json;

public sealed partial class PostgresManufacturingStore : IManufacturingLegacyStore
{
    public (ManufacturingDeviationDto? Deviation, string? Error) CreateDeviation(
        Guid productionBatchId, string tenantKey, CreateDeviationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Type) || string.IsNullOrWhiteSpace(request.Description) ||
            string.IsNullOrWhiteSpace(request.Impact) || string.IsNullOrWhiteSpace(request.RequestedBy))
            return (null, "invalid_deviation");

        using var db = dbFactory.CreateDbContext();
        var batch = db.ProductionBatches.SingleOrDefault(x => x.Id == productionBatchId);
        if (batch is null) return (null, "production_batch_not_found");
        if (!batch.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_scope_denied");
        if (batch.Status is "Completed" or "Cancelled" or "Closed") return (null, "batch_not_active");

        var now = DateTimeOffset.UtcNow;
        var entity = new ManufacturingDeviationEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, ProductionBatchId = productionBatchId,
            Type = request.Type.Trim(), Description = request.Description.Trim(), Impact = request.Impact.Trim(),
            Status = "Requested", RequestedBy = request.RequestedBy.Trim(), CreatedAt = now
        };
        db.Deviations.Add(entity);
        AddDeviationEvent(db, entity, "Raised", entity.RequestedBy, now);
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    public IReadOnlyList<ManufacturingDeviationDto> GetDeviations(
        string tenantKey, Guid? productionBatchId, string? status, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.Deviations.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (productionBatchId.HasValue) query = query.Where(x => x.ProductionBatchId == productionBatchId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return query.OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(limit, 1, 200)).AsEnumerable().Select(ToDto).ToList();
    }

    public (ManufacturingDeviationDto? Deviation, string? Error) ChangeDeviationStatus(
        Guid deviationId, string tenantKey, string targetStatus, DeviationActionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Actor)) return (null, "invalid_deviation_actor");
        using var db = dbFactory.CreateDbContext();
        var entity = db.Deviations.SingleOrDefault(x => x.Id == deviationId);
        if (entity is null) return (null, "deviation_not_found");
        if (!entity.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_scope_denied");

        var actor = request.Actor.Trim();
        var valid = (entity.Status, targetStatus) switch
        {
            ("Requested", "Approved") => !entity.RequestedBy.Equals(actor, StringComparison.OrdinalIgnoreCase),
            ("Requested", "Rejected") => !entity.RequestedBy.Equals(actor, StringComparison.OrdinalIgnoreCase),
            ("Approved", "Closed") => true,
            _ => false
        };
        if (!valid)
            return (null, targetStatus is "Approved" or "Rejected" && entity.RequestedBy.Equals(actor, StringComparison.OrdinalIgnoreCase)
                ? "author_cannot_approve_own_deviation"
                : "invalid_deviation_transition");

        var now = DateTimeOffset.UtcNow;
        entity.Status = targetStatus;
        entity.ResolutionNotes = request.Notes?.Trim();
        if (targetStatus is "Approved" or "Rejected")
        {
            entity.ApprovedBy = actor;
            entity.ApprovedAt = now;
        }
        if (targetStatus == "Closed") entity.ClosedAt = now;
        AddDeviationEvent(db, entity, targetStatus, actor, now);
        db.SaveChanges();
        return (ToDto(entity), null);
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
            Status = "Pending"
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
