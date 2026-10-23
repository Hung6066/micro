using Microsoft.EntityFrameworkCore;
using His.Hope.ManufacturingService.Application.Ports;

public sealed partial class PostgresManufacturingStore : IManufacturingCapaStore
{
    public async Task<(CapaDto? Capa, string? Error)> CreateCapaAsync(string tenantKey, CreateCapaRequest request, string actor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.ProblemDescription) || string.IsNullOrWhiteSpace(request.Owner)) return (null, "invalid_capa");
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (request.SupplierId.HasValue && !await db.Suppliers.AnyAsync(x => x.Id == request.SupplierId.Value && x.TenantKey == tenantKey && x.Active, cancellationToken)) return (null, ManufacturingErrorCodes.SupplierNotFound);
        if (request.DeviationId.HasValue && !await db.Deviations.AnyAsync(x => x.Id == request.DeviationId.Value && x.TenantKey == tenantKey, cancellationToken)) return (null, ManufacturingErrorCodes.DeviationNotFound);
        var now = DateTimeOffset.UtcNow;
        var entity = new ManufacturingCapaEntity { Id = Guid.NewGuid(), TenantKey = tenantKey, DeviationId = request.DeviationId, SupplierId = request.SupplierId, Title = request.Title.Trim(), ProblemDescription = request.ProblemDescription.Trim(), RootCause = request.RootCause?.Trim() ?? "", CorrectiveAction = request.CorrectiveAction?.Trim() ?? "", PreventiveAction = request.PreventiveAction?.Trim() ?? "", Owner = request.Owner.Trim(), Status = "Open", DueAt = request.DueAt, CreatedAt = now };
        db.Capas.Add(entity); AddAudit(db, tenantKey, "Capa", entity.Id, ManufacturingStatusCodes.Created, actor, entity.Title, now); await db.SaveChangesAsync(cancellationToken); return (ToDto(entity), null);
    }

    public async Task<IReadOnlyList<CapaDto>> GetCapasAsync(string tenantKey, string? status, int limit, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken); var query = db.Capas.AsNoTracking().Where(x => x.TenantKey == tenantKey); if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status); return (await query.OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(limit, 1, 200)).ToListAsync(cancellationToken)).Select(ToDto).ToList();
    }

    public async Task<(CapaDto? Capa, string? Error)> UpdateCapaStatusAsync(string tenantKey, Guid capaId, UpdateCapaStatusRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Actor)) return (null, "invalid_capa_actor");
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken); var entity = await db.Capas.SingleOrDefaultAsync(x => x.Id == capaId && x.TenantKey == tenantKey, cancellationToken); if (entity is null) return (null, "capa_not_found");
        var next = request.Status.Trim(); var valid = (entity.Status, next) switch { ("Open", "InProgress") or ("InProgress", "Verified") or ("Verified", ManufacturingStatusCodes.Closed) => true, _ => false }; if (!valid) return (null, "invalid_capa_transition");
        entity.Status = next; if (next == ManufacturingStatusCodes.Closed) entity.ClosedAt = DateTimeOffset.UtcNow; AddAudit(db, tenantKey, "Capa", entity.Id, next, request.Actor.Trim(), request.Notes?.Trim() ?? "", DateTimeOffset.UtcNow); await db.SaveChangesAsync(cancellationToken); return (ToDto(entity), null);
    }

    public async Task<(SupplierEvaluationDto? Evaluation, string? Error)> CreateSupplierEvaluationAsync(string tenantKey, CreateSupplierEvaluationRequest request, string actor, CancellationToken cancellationToken)
    {
        if (request.Score is < 1 or > 5 || string.IsNullOrWhiteSpace(actor)) return (null, "invalid_supplier_evaluation");
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken); if (!await db.Suppliers.AnyAsync(x => x.Id == request.SupplierId && x.TenantKey == tenantKey, cancellationToken)) return (null, ManufacturingErrorCodes.SupplierNotFound);
        var entity = new ManufacturingSupplierEvaluationEntity { Id = Guid.NewGuid(), TenantKey = tenantKey, SupplierId = request.SupplierId, Score = request.Score, QualityNotes = request.QualityNotes?.Trim(), DeliveryNotes = request.DeliveryNotes?.Trim(), Notes = request.Notes?.Trim(), EvaluatedBy = actor.Trim(), EvaluatedAt = DateTimeOffset.UtcNow };
        db.SupplierEvaluations.Add(entity); AddAudit(db, tenantKey, "SupplierEvaluation", entity.Id, ManufacturingStatusCodes.Created, actor, $"score={entity.Score}", entity.EvaluatedAt); await db.SaveChangesAsync(cancellationToken); return (ToDto(entity), null);
    }

    public async Task<IReadOnlyList<SupplierEvaluationDto>> GetSupplierEvaluationsAsync(string tenantKey, Guid? supplierId, int limit, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken); var query = db.SupplierEvaluations.AsNoTracking().Where(x => x.TenantKey == tenantKey); if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value); return (await query.OrderByDescending(x => x.EvaluatedAt).Take(Math.Clamp(limit, 1, 200)).ToListAsync(cancellationToken)).Select(ToDto).ToList();
    }

    private static void AddAudit(ManufacturingDbContext db, string tenantKey, string entityType, Guid entityId, string action, string actor, string details, DateTimeOffset occurredAt) => db.AuditEvents.Add(new ManufacturingAuditEventEntity { Id = Guid.NewGuid(), TenantKey = tenantKey, EntityType = entityType, EntityId = entityId, Action = action, Actor = actor, Details = details, OccurredAt = occurredAt });
    private static CapaDto ToDto(ManufacturingCapaEntity x) => new(x.Id, x.TenantKey, x.DeviationId, x.SupplierId, x.Title, x.ProblemDescription, x.RootCause, x.CorrectiveAction, x.PreventiveAction, x.Owner, x.Status, x.DueAt, x.CreatedAt, x.ClosedAt);
    private static SupplierEvaluationDto ToDto(ManufacturingSupplierEvaluationEntity x) => new(x.Id, x.TenantKey, x.SupplierId, x.Score, x.QualityNotes, x.DeliveryNotes, x.Notes, x.EvaluatedBy, x.EvaluatedAt);
}

public sealed class ManufacturingCapaEntity { public Guid Id { get; set; } public string TenantKey { get; set; } = ""; public Guid? DeviationId { get; set; } public Guid? SupplierId { get; set; } public string Title { get; set; } = ""; public string ProblemDescription { get; set; } = ""; public string RootCause { get; set; } = ""; public string CorrectiveAction { get; set; } = ""; public string PreventiveAction { get; set; } = ""; public string Owner { get; set; } = ""; public string Status { get; set; } = "Open"; public DateTimeOffset? DueAt { get; set; } public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset? ClosedAt { get; set; } }
public sealed class ManufacturingSupplierEvaluationEntity { public Guid Id { get; set; } public string TenantKey { get; set; } = ""; public Guid SupplierId { get; set; } public int Score { get; set; } public string? QualityNotes { get; set; } public string? DeliveryNotes { get; set; } public string? Notes { get; set; } public string EvaluatedBy { get; set; } = ""; public DateTimeOffset EvaluatedAt { get; set; } }
public sealed class ManufacturingAuditEventEntity { public Guid Id { get; set; } public string TenantKey { get; set; } = ""; public string EntityType { get; set; } = ""; public Guid EntityId { get; set; } public string Action { get; set; } = ""; public string Actor { get; set; } = ""; public string Details { get; set; } = ""; public DateTimeOffset OccurredAt { get; set; } }
