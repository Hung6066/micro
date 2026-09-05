using Microsoft.EntityFrameworkCore;
using His.Hope.Contracts.Manufacturing;

public sealed class ManufacturingEntityStatusHistoryEntity
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public string TenantKey { get; set; } = "";
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public string Actor { get; set; } = "system";
    public DateTimeOffset OccurredAt { get; set; }
}

internal static class EntityStatusHistoryStore
{
    internal static void Append(
        ManufacturingDbContext db,
        string entityType,
        Guid entityId,
        string tenantKey,
        string fromStatus,
        string toStatus,
        string actor,
        DateTimeOffset occurredAt)
    {
        if (fromStatus.Equals(toStatus, StringComparison.OrdinalIgnoreCase))
            return;

        db.EntityStatusHistory.Add(new ManufacturingEntityStatusHistoryEntity
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            TenantKey = tenantKey,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Actor = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim(),
            OccurredAt = occurredAt,
        });
    }

    internal static IReadOnlyList<EntityStatusHistoryDto> Get(
        ManufacturingDbContext db,
        string tenantKey,
        string entityType,
        Guid entityId)
    {
        return db.EntityStatusHistory.AsNoTracking()
            .Where(x =>
                x.TenantKey == tenantKey &&
                x.EntityType == entityType &&
                x.EntityId == entityId)
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.Id)
            .Select(x => new EntityStatusHistoryDto(
                x.Id,
                x.EntityType,
                x.EntityId,
                x.TenantKey,
                x.FromStatus,
                x.ToStatus,
                x.Actor,
                x.OccurredAt))
            .ToList();
    }

    internal static async Task<IReadOnlyList<EntityStatusHistoryDto>> GetAsync(
        ManufacturingDbContext db,
        string tenantKey,
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        return await db.EntityStatusHistory.AsNoTracking()
            .Where(x =>
                x.TenantKey == tenantKey &&
                x.EntityType == entityType &&
                x.EntityId == entityId)
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.Id)
            .Select(x => new EntityStatusHistoryDto(
                x.Id,
                x.EntityType,
                x.EntityId,
                x.TenantKey,
                x.FromStatus,
                x.ToStatus,
                x.Actor,
                x.OccurredAt))
            .ToListAsync(cancellationToken);
    }
}
