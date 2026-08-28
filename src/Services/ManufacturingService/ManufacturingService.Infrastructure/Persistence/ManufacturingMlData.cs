using System.Text.Json;
using His.Hope.Contracts.Manufacturing;
using His.Hope.ManufacturingService.Application.Ports;
using Microsoft.EntityFrameworkCore;

public sealed class ManufacturingMlDataStore(IDbContextFactory<ManufacturingDbContext> dbFactory) : IManufacturingMlDataStore
{
    public (OperationMeasurementDto? Measurement, string? Error) RecordOperationMeasurement(string tenantKey, string actor, RecordOperationMeasurementRequest request)
    {
        if (request.ProductionBatchId == Guid.Empty || string.IsNullOrWhiteSpace(request.MeasurementType) || string.IsNullOrWhiteSpace(request.Uom) || request.Value < 0 || request.MeasuredAt == default)
            return (null, "invalid_operation_measurement");
        using var db = dbFactory.CreateDbContext();
        var batch = db.ProductionBatches.SingleOrDefault(x => x.Id == request.ProductionBatchId && x.TenantKey == tenantKey);
        if (batch is null) return (null, "production_batch_not_found");
        var entity = new ManufacturingOperationMeasurementEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, ProductionBatchId = request.ProductionBatchId,
            OperationExecutionId = request.OperationExecutionId, MachineId = request.MachineId, LotId = request.LotId,
            MeasurementType = request.MeasurementType.Trim(), Value = request.Value, Uom = request.Uom.Trim(),
            MeasuredAt = request.MeasuredAt, RecordedBy = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim(),
            Source = string.IsNullOrWhiteSpace(request.Source) ? "operator" : request.Source.Trim(), Sequence = request.Sequence,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(), CreatedAt = DateTimeOffset.UtcNow
        };
        db.OperationMeasurements.Add(entity);
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = entity.Id, Type = "Manufacturing.OperationMeasurementRecorded.v1", OccurredOn = entity.MeasuredAt.UtcDateTime,
            Status = "Pending", Content = JsonSerializer.Serialize(new { eventId = entity.Id, eventType = "Manufacturing.OperationMeasurementRecorded.v1", schemaVersion = 1, occurredAt = entity.MeasuredAt, aggregateId = entity.ProductionBatchId, tenantKey, source = entity.Source })
        });
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    public IReadOnlyList<OperationMeasurementDto> GetOperationMeasurements(string tenantKey, Guid productionBatchId, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        return db.OperationMeasurements.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.ProductionBatchId == productionBatchId)
            .OrderBy(x => x.MeasuredAt).Take(Math.Clamp(limit, 1, 5_000)).AsEnumerable().Select(ToDto).ToList();
    }

    public (SalesActualDto? Actual, string? Error) RecordSalesActual(string tenantKey, string actor, RecordSalesActualRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProductSku) || request.PeriodEnd < request.PeriodStart || request.Quantity < 0 || string.IsNullOrWhiteSpace(request.Uom) || string.IsNullOrWhiteSpace(request.Channel))
            return (null, "invalid_sales_actual");
        using var db = dbFactory.CreateDbContext();
        var entity = new ManufacturingSalesActualEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, ProductSku = request.ProductSku.Trim(), PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd, Quantity = request.Quantity, Uom = request.Uom.Trim(), Channel = request.Channel.Trim(),
            Region = string.IsNullOrWhiteSpace(request.Region) ? null : request.Region.Trim(), Source = string.IsNullOrWhiteSpace(request.Source) ? "sales" : request.Source.Trim(),
            Actor = string.IsNullOrWhiteSpace(actor) ? (string.IsNullOrWhiteSpace(request.Actor) ? "system" : request.Actor.Trim()) : actor.Trim(), CreatedAt = DateTimeOffset.UtcNow
        };
        db.SalesActuals.Add(entity);
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    public IReadOnlyList<SalesActualDto> GetSalesActuals(string tenantKey, string? productSku, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.SalesActuals.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(productSku)) query = query.Where(x => x.ProductSku == productSku.Trim());
        return query.OrderByDescending(x => x.PeriodStart).Take(Math.Clamp(limit, 1, 5_000)).AsEnumerable().Select(ToDto).ToList();
    }

    public (MlFeatureSnapshotDto? Snapshot, string? Error) CreateFeatureSnapshot(string tenantKey, string actor, MlFeatureSnapshotRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DatasetKey) || string.IsNullOrWhiteSpace(request.EntityType) || request.EntityId == Guid.Empty || request.AsOf == default || request.SchemaVersion <= 0 || string.IsNullOrWhiteSpace(request.FeaturesJson))
            return (null, "invalid_ml_feature_snapshot");
        try
        {
            using var features = JsonDocument.Parse(request.FeaturesJson);
            if (request.LabelJson is not null)
            {
                using var label = JsonDocument.Parse(request.LabelJson);
            }
            if (request.SourceEventIdsJson is not null)
            {
                using var sourceEvents = JsonDocument.Parse(request.SourceEventIdsJson);
            }
        }
        catch (JsonException) { return (null, "invalid_ml_json"); }
        using var db = dbFactory.CreateDbContext();
        var duplicate = db.MlFeatureSnapshots.Any(x => x.TenantKey == tenantKey && x.DatasetKey == request.DatasetKey.Trim() && x.EntityType == request.EntityType.Trim() && x.EntityId == request.EntityId && x.AsOf == request.AsOf && x.SchemaVersion == request.SchemaVersion);
        if (duplicate) return (null, "ml_feature_snapshot_exists");
        var entity = new ManufacturingMlFeatureSnapshotEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, DatasetKey = request.DatasetKey.Trim(), EntityType = request.EntityType.Trim(),
            EntityId = request.EntityId, AsOf = request.AsOf, FeaturesJson = request.FeaturesJson, LabelJson = request.LabelJson,
            SourceEventIdsJson = request.SourceEventIdsJson, Split = string.IsNullOrWhiteSpace(request.Split) ? "unassigned" : request.Split.Trim().ToLowerInvariant(),
            SchemaVersion = request.SchemaVersion, CreatedBy = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim(), CreatedAt = DateTimeOffset.UtcNow
        };
        db.MlFeatureSnapshots.Add(entity);
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    public IReadOnlyList<MlFeatureSnapshotDto> GetFeatureSnapshots(string tenantKey, string datasetKey, DateTimeOffset? from, DateTimeOffset? to, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.MlFeatureSnapshots.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.DatasetKey == datasetKey.Trim());
        if (from.HasValue) query = query.Where(x => x.AsOf >= from.Value);
        if (to.HasValue) query = query.Where(x => x.AsOf <= to.Value);
        return query.OrderBy(x => x.AsOf).Take(Math.Clamp(limit, 1, 50_000)).AsEnumerable().Select(ToDto).ToList();
    }

    public MlDatasetQualityDto GetDatasetQuality(string tenantKey, string datasetKey)
    {
        using var db = dbFactory.CreateDbContext();
        var rows = db.MlFeatureSnapshots.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.DatasetKey == datasetKey.Trim()).ToList();
        var warnings = new List<string>();
        if (rows.Count == 0) warnings.Add("dataset_empty");
        if (rows.Any(x => x.Split == "unassigned")) warnings.Add("split_unassigned");
        if (rows.Select(x => x.SchemaVersion).Distinct().Count() > 1) warnings.Add("mixed_schema_versions");
        var version = rows.Count == 0 ? 0 : rows.GroupBy(x => x.SchemaVersion).OrderByDescending(x => x.Count()).First().Key;
        return new(tenantKey, datasetKey.Trim(), rows.Count, rows.Count(x => x.LabelJson is not null), version, rows.Count == 0 ? DateTimeOffset.UtcNow : rows.Max(x => x.AsOf), warnings);
    }

    private static OperationMeasurementDto ToDto(ManufacturingOperationMeasurementEntity x) => new(x.Id, x.TenantKey, x.ProductionBatchId, x.OperationExecutionId, x.MachineId, x.LotId, x.MeasurementType, x.Value, x.Uom, x.MeasuredAt, x.RecordedBy, x.Source, x.Sequence, x.Notes, x.CreatedAt);
    private static SalesActualDto ToDto(ManufacturingSalesActualEntity x) => new(x.Id, x.TenantKey, x.ProductSku, x.PeriodStart, x.PeriodEnd, x.Quantity, x.Uom, x.Channel, x.Region, x.Source, x.Actor, x.CreatedAt);
    private static MlFeatureSnapshotDto ToDto(ManufacturingMlFeatureSnapshotEntity x) => new(x.Id, x.TenantKey, x.DatasetKey, x.EntityType, x.EntityId, x.AsOf, x.FeaturesJson, x.LabelJson, x.SourceEventIdsJson, x.Split, x.SchemaVersion, x.CreatedAt);
}

public sealed class ManufacturingOperationMeasurementEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public Guid ProductionBatchId { get; set; }
    public Guid? OperationExecutionId { get; set; }
    public Guid? MachineId { get; set; }
    public Guid? LotId { get; set; }
    public string MeasurementType { get; set; } = "";
    public decimal Value { get; set; }
    public string Uom { get; set; } = "";
    public DateTimeOffset MeasuredAt { get; set; }
    public string RecordedBy { get; set; } = "";
    public string Source { get; set; } = "operator";
    public long? Sequence { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ManufacturingSalesActualEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string ProductSku { get; set; } = "";
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = "";
    public string Channel { get; set; } = "unknown";
    public string? Region { get; set; }
    public string Source { get; set; } = "sales";
    public string Actor { get; set; } = "system";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ManufacturingMlFeatureSnapshotEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string DatasetKey { get; set; } = "";
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public DateTimeOffset AsOf { get; set; }
    public string FeaturesJson { get; set; } = "{}";
    public string? LabelJson { get; set; }
    public string? SourceEventIdsJson { get; set; }
    public string Split { get; set; } = "unassigned";
    public int SchemaVersion { get; set; } = 1;
    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset CreatedAt { get; set; }
}
