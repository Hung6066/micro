using System.Text.Json;
using His.Hope.ManufacturingService.Domain;
using His.Hope.ManufacturingService.Application.Ports;
using Microsoft.EntityFrameworkCore;
using His.Hope.Persistence.Querying;

public sealed partial class PostgresManufacturingStore
{
    public async Task<(QualityInspectionDto? Inspection, string? Error)> CreateQualityInspectionAsync(CreateQualityInspectionRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedStatus = request.Status.Trim();
        if (!AllowedInspectionStatuses.Contains(normalizedStatus, StringComparer.OrdinalIgnoreCase)) return (null, "invalid_inspection_status");
        if (request.MoisturePercent is < 0 or > 100) return (null, "invalid_moisture_percent");
        var testResultPolicyError = QualityInspectionPolicy.Validate(
            request.Results?.Select(x => new QualityTestResultInput(x.TestCode, x.TestName, x.MeasuredValue, x.Uom, x.Result, x.LowerLimit, x.UpperLimit, x.Method, x.EvidenceReference)).ToList(),
            normalizedStatus);
        if (testResultPolicyError is not null) return (null, testResultPolicyError);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var lot = await db.Lots.SingleOrDefaultAsync(x => x.Id == request.LotId, cancellationToken);
        if (lot is null) return (null, ManufacturingErrorCodes.LotNotFound);
        if (!lot.TenantKey.Equals(request.TenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantMismatch);
        ManufacturingInspectionPlanVersionEntity? inspectionPlan = null;
        if (request.InspectionPlanVersionId.HasValue)
        {
            var now = request.InspectedAt ?? DateTimeOffset.UtcNow;
            inspectionPlan = await db.InspectionPlanVersions.SingleOrDefaultAsync(x => x.Id == request.InspectionPlanVersionId.Value, cancellationToken);
            if (inspectionPlan is null) return (null, "inspection_plan_not_found");
            if (!inspectionPlan.TenantKey.Equals(lot.TenantKey, StringComparison.OrdinalIgnoreCase) || !inspectionPlan.ProductSku.Equals(lot.Sku, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.InspectionPlanMismatch);
            if (!inspectionPlan.Status.Equals(ManufacturingStatusCodes.Approved, StringComparison.OrdinalIgnoreCase) || (inspectionPlan.EffectiveFrom.HasValue && inspectionPlan.EffectiveFrom > now) || (inspectionPlan.EffectiveTo.HasValue && inspectionPlan.EffectiveTo <= now)) return (null, "inspection_plan_not_effective");
        }
        var entity = await db.QualityInspections
            .Where(x => x.LotId == lot.Id && x.TenantKey == lot.TenantKey && x.Status == ManufacturingStatusCodes.Pending)
            .OrderByDescending(x => x.InspectedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (entity is null)
        {
            entity = new ManufacturingQualityInspectionEntity
            {
                Id = Guid.NewGuid(), LotId = lot.Id, TenantKey = lot.TenantKey
            };
            db.QualityInspections.Add(entity);
        }
        entity.Status = normalizedStatus;
        entity.MoisturePercent = request.MoisturePercent;
        entity.Inspector = request.Inspector.Trim();
        entity.Notes = request.Notes?.Trim();
        entity.InspectedAt = request.InspectedAt ?? DateTimeOffset.UtcNow;
        entity.SpecificationReference = request.SpecificationReference?.Trim();
        entity.InspectionPlanVersionId = inspectionPlan?.Id;
        var testResults = request.Results?.Select(x => new ManufacturingQualityTestResultEntity
        {
            Id = Guid.NewGuid(), QualityInspectionId = entity.Id, TestCode = x.TestCode.Trim(), TestName = x.TestName.Trim(),
            MeasuredValue = x.MeasuredValue, Uom = x.Uom.Trim(), Result = x.Result.Trim(), LowerLimit = x.LowerLimit,
            UpperLimit = x.UpperLimit, Method = x.Method?.Trim(), EvidenceReference = x.EvidenceReference?.Trim()
        }).ToList() ?? [];
        var existingTestResults = await db.QualityTestResults.Where(x => x.QualityInspectionId == entity.Id).ToListAsync(cancellationToken);
        if (existingTestResults.Count > 0) db.QualityTestResults.RemoveRange(existingTestResults);
        if (testResults.Count > 0) db.QualityTestResults.AddRange(testResults);
        lot.QualityStatus = normalizedStatus.Equals("Pass", StringComparison.OrdinalIgnoreCase) ? "Passed" :
            normalizedStatus.Equals("Fail", StringComparison.OrdinalIgnoreCase) ? "Failed" : ManufacturingStatusCodes.Pending;
        lot.UpdatedAt = entity.InspectedAt;
        var dispositionChanged = false;
        var dispositionEventId = Guid.NewGuid();
        var previousDisposition = lot.Disposition;
        if (normalizedStatus.Equals("Pass", StringComparison.OrdinalIgnoreCase) &&
            (lot.Disposition.Equals("Quarantined", StringComparison.OrdinalIgnoreCase) || lot.Disposition.Equals("Hold", StringComparison.OrdinalIgnoreCase)))
        {
            lot.Disposition = ManufacturingStatusCodes.Released;
            dispositionChanged = true;
        }
        else if (normalizedStatus.Equals("Fail", StringComparison.OrdinalIgnoreCase) &&
                 lot.Disposition.Equals(ManufacturingStatusCodes.Released, StringComparison.OrdinalIgnoreCase))
        {
            lot.Disposition = "Quarantined";
            dispositionChanged = true;
        }
        if (dispositionChanged)
        {
            db.LotStatusHistory.Add(new ManufacturingLotStatusHistoryEntity
            {
                Id = Guid.NewGuid(), LotId = lot.Id, TenantKey = lot.TenantKey, FromDisposition = previousDisposition,
                ToDisposition = lot.Disposition, Actor = entity.Inspector, ReasonCode = "quality_inspection",
                CorrelationId = entity.Id, OccurredAt = entity.InspectedAt
            });
            db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
            {
                Id = Guid.NewGuid(), TenantKey = lot.TenantKey, LotId = lot.Id,
                TransactionType = lot.Disposition.Equals(ManufacturingStatusCodes.Released, StringComparison.OrdinalIgnoreCase) ? "Release" : "Hold",
                Quantity = lot.Quantity, Uom = lot.Uom, FacilityId = "default", StockStatus = lot.Disposition,
                CorrelationId = entity.Id, OccurredAt = entity.InspectedAt
            });
            db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
            {
                Id = Guid.NewGuid(), Type = "Manufacturing.LotDispositionChanged.v1",
                Content = JsonSerializer.Serialize(new { eventId = dispositionEventId, schemaVersion = 1, occurredAt = entity.InspectedAt, correlationId = entity.Id, facilityId = "default", lotId = lot.Id, tenantKey = lot.TenantKey, disposition = lot.Disposition, reason = "quality_inspection" }),
                OccurredOn = entity.InspectedAt.UtcDateTime, Status = ManufacturingStatusCodes.Pending
            });
        }
        var inspectionEventId = Guid.NewGuid();
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = inspectionEventId, Type = "Manufacturing.QualityInspectionRecorded.v1",
            Content = JsonSerializer.Serialize(new { eventId = inspectionEventId, schemaVersion = 1, occurredAt = DateTimeOffset.UtcNow, correlationId = inspectionEventId, facilityId = (string?)null, inspectionId = entity.Id, lotId = entity.LotId, tenantKey = entity.TenantKey, status = entity.Status, moisturePercent = entity.MoisturePercent, specificationReference = entity.SpecificationReference, resultCount = testResults.Count, failedResultCount = testResults.Count(x => x.Result.Equals("Fail", StringComparison.OrdinalIgnoreCase)) }),
            OccurredOn = DateTime.UtcNow, Status = ManufacturingStatusCodes.Pending
        });
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(entity, testResults), null);
    }

    public async Task<(QualitySampleDto? Sample, string? Error)> CreateQualitySampleAsync(CreateQualitySampleRequest request, string tenantKey, CancellationToken cancellationToken = default)
    {
        if (request.InspectionId == Guid.Empty || string.IsNullOrWhiteSpace(request.SampleCode) || string.IsNullOrWhiteSpace(request.CollectedBy)) return (null, "invalid_quality_sample");
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var inspection = await db.QualityInspections.SingleOrDefaultAsync(x => x.Id == request.InspectionId, cancellationToken);
        if (inspection is null) return (null, ManufacturingErrorCodes.QualityInspectionNotFound);
        if (!inspection.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantScopeDenied);
        if (await db.QualitySamples.AnyAsync(x => x.TenantKey == tenantKey && x.InspectionId == request.InspectionId && x.SampleCode == request.SampleCode.Trim(), cancellationToken)) return (null, ManufacturingErrorCodes.QualitySampleExists);
        var entity = new ManufacturingQualitySampleEntity { Id = Guid.NewGuid(), InspectionId = inspection.Id, LotId = inspection.LotId, TenantKey = tenantKey, SampleCode = request.SampleCode.Trim(), CollectedBy = request.CollectedBy.Trim(), CollectedAt = request.CollectedAt ?? DateTimeOffset.UtcNow, Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(), Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(), Disposition = ManufacturingStatusCodes.Pending, CreatedAt = DateTimeOffset.UtcNow };
        db.QualitySamples.Add(entity); await db.SaveChangesAsync(cancellationToken);
        return (ToDto(entity), null);
    }

    public IReadOnlyList<QualitySampleDto> GetQualitySamples(string tenantKey, Guid? inspectionId, string? disposition, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.QualitySamples.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (inspectionId.HasValue) query = query.Where(x => x.InspectionId == inspectionId.Value);
        if (!string.IsNullOrWhiteSpace(disposition)) query = query.Where(x => x.Disposition == disposition);
        return query.OrderByDescending(x => x.CollectedAt).Take(Math.Clamp(limit, 1, 200)).AsEnumerable().Select(ToDto).ToList();
    }

    public async Task<(QualitySampleDto? Sample, string? Error)> ChangeQualitySampleDispositionAsync(Guid sampleId, string tenantKey, QualitySampleDispositionRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.QualitySamples.SingleOrDefaultAsync(x => x.Id == sampleId, cancellationToken);
        if (entity is null) return (null, ManufacturingErrorCodes.QualitySampleNotFound);
        if (!entity.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantScopeDenied);
        if (string.IsNullOrWhiteSpace(request.Actor)) return (null, "invalid_quality_sample_actor");
        var target = request.Disposition.Trim();
        if (target is not ("Accepted" or ManufacturingStatusCodes.Rejected or "Hold") || entity.Disposition != ManufacturingStatusCodes.Pending) return (null, "invalid_quality_sample_disposition");
        entity.Disposition = target; entity.DispositionReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(); entity.DisposedBy = request.Actor.Trim(); entity.DisposedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(entity), null);
    }

    public async Task<(InspectionPlanVersionDto? Plan, string? Error)> CreateInspectionPlanVersionAsync(CreateInspectionPlanVersionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.PlanCode) || string.IsNullOrWhiteSpace(request.ProductSku) || request.Version <= 0 || string.IsNullOrWhiteSpace(request.SamplingMethod) || string.IsNullOrWhiteSpace(request.SamplingFrequency) || string.IsNullOrWhiteSpace(request.AcceptanceCriteria)) return (null, "invalid_inspection_plan");
        var status = request.Status.Trim();
        if (status is not (ManufacturingStatusCodes.Draft or ManufacturingStatusCodes.Submitted)) return (null, "invalid_inspection_plan_status");
        if (request.EffectiveTo is not null && request.EffectiveFrom is not null && request.EffectiveTo <= request.EffectiveFrom) return (null, "invalid_inspection_plan_dates");
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.InspectionPlanVersions.AnyAsync(x => x.TenantKey == request.TenantKey.Trim() && x.PlanCode == request.PlanCode.Trim() && x.Version == request.Version, cancellationToken)) return (null, "inspection_plan_exists");
        var entity = new ManufacturingInspectionPlanVersionEntity { Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), PlanCode = request.PlanCode.Trim(), ProductSku = request.ProductSku.Trim(), Version = request.Version, SamplingMethod = request.SamplingMethod.Trim(), SamplingFrequency = request.SamplingFrequency.Trim(), AcceptanceCriteria = request.AcceptanceCriteria.Trim(), Status = status, EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo, CreatedBy = string.IsNullOrWhiteSpace(request.CreatedBy) ? null : request.CreatedBy.Trim(), CreatedAt = DateTimeOffset.UtcNow };
        db.InspectionPlanVersions.Add(entity); await db.SaveChangesAsync(cancellationToken);
        return (ToDto(entity), null);
    }

    public IReadOnlyList<InspectionPlanVersionDto> GetInspectionPlanVersions(string tenantKey, string? productSku, string? status, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.InspectionPlanVersions.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(productSku)) query = query.Where(x => x.ProductSku == productSku);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return query.OrderByDescending(x => x.ProductSku).ThenByDescending(x => x.Version).Take(Math.Clamp(limit, 1, 200)).AsEnumerable().Select(ToDto).ToList();
    }

    public async Task<(InspectionPlanVersionDto? Plan, string? Error)> ChangeInspectionPlanLifecycleAsync(Guid planId, string tenantKey, string targetStatus, InspectionPlanLifecycleRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.InspectionPlanVersions.SingleOrDefaultAsync(x => x.Id == planId, cancellationToken);
        if (entity is null) return (null, "inspection_plan_not_found");
        if (!entity.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantScopeDenied);
        if (string.IsNullOrWhiteSpace(request.Actor)) return (null, "invalid_inspection_plan_actor");
        var target = targetStatus.Trim();
        var valid = (entity.Status, target) switch { (ManufacturingStatusCodes.Draft, ManufacturingStatusCodes.Submitted) => true, (ManufacturingStatusCodes.Submitted, ManufacturingStatusCodes.Approved) => true, (ManufacturingStatusCodes.Approved, "Retired") => true, _ => false };
        if (!valid) return (null, "invalid_inspection_plan_transition");
        entity.Status = target;
        if (target == ManufacturingStatusCodes.Approved) { entity.ApprovedBy = request.Actor.Trim(); entity.ApprovedAt = DateTimeOffset.UtcNow; entity.EffectiveFrom = request.EffectiveFrom ?? entity.EffectiveFrom ?? DateTimeOffset.UtcNow; entity.EffectiveTo = request.EffectiveTo ?? entity.EffectiveTo; }
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(entity), null);
    }

    public async Task<IReadOnlyList<QualityInspectionDto>> GetQualityInspectionsAsync(Guid lotId, string? tenantKey, int limit, int page = 1, CancellationToken cancellationToken = default)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.QualityInspections.AsNoTracking().Where(x => x.LotId == lotId);
        if (!string.IsNullOrWhiteSpace(tenantKey)) query = query.Where(x => x.TenantKey == tenantKey);
        var inspections = await query.TagUseCase("Manufacturing.Quality.GetInspections")
            .OrderByDescending(x => x.InspectedAt).ApplyPage(page, limit, 100).ToListAsync(cancellationToken);
        var resultsByInspection = (await db.QualityTestResults.AsNoTracking()
            .TagUseCase("Manufacturing.Quality.GetInspectionResults")
            .Where(x => inspections.Select(inspection => inspection.Id).Contains(x.QualityInspectionId))
            .OrderBy(x => x.TestCode).ToListAsync(cancellationToken))
            .GroupBy(x => x.QualityInspectionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ManufacturingQualityTestResultEntity>)group.ToList());
        return inspections.Select(inspection => ToDto(inspection, resultsByInspection.GetValueOrDefault(inspection.Id, []))).ToList();
    }

    private static readonly string[] AllowedInspectionStatuses = ["Pass", "Fail", ManufacturingStatusCodes.Pending];
}
