using Microsoft.EntityFrameworkCore;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.ManufacturingService.Domain;
using His.Hope.Infrastructure.DataLifecycle;
using His.Hope.Persistence.Querying;
using System.Text.Json;
using System.Data;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.AspNetCore.Tenancy;

public sealed class ManufacturingDbContext(DbContextOptions<ManufacturingDbContext> options) : DbContext(options)
{
    public DbSet<ManufacturingLotEntity> Lots => Set<ManufacturingLotEntity>();
    public DbSet<ManufacturingLotStatusHistoryEntity> LotStatusHistory => Set<ManufacturingLotStatusHistoryEntity>();
    public DbSet<ManufacturingEntityStatusHistoryEntity> EntityStatusHistory => Set<ManufacturingEntityStatusHistoryEntity>();
    public DbSet<ManufacturingTransformationEntity> Transformations => Set<ManufacturingTransformationEntity>();
    public DbSet<ManufacturingTransformationInputEntity> TransformationInputs => Set<ManufacturingTransformationInputEntity>();
    public DbSet<ManufacturingOutboxMessageEntity> OutboxMessages => Set<ManufacturingOutboxMessageEntity>();
    public DbSet<ManufacturingEventReceiptEntity> EventReceipts => Set<ManufacturingEventReceiptEntity>();
    public DbSet<ManufacturingRecipeEntity> Recipes => Set<ManufacturingRecipeEntity>();
    public DbSet<ManufacturingRecipeComponentEntity> RecipeComponents => Set<ManufacturingRecipeComponentEntity>();
    public DbSet<ManufacturingMachineEntity> Machines => Set<ManufacturingMachineEntity>();
    public DbSet<ManufacturingMachineCalibrationEntity> MachineCalibrations => Set<ManufacturingMachineCalibrationEntity>();
    public DbSet<ManufacturingMachineDowntimeEntity> MachineDowntimes => Set<ManufacturingMachineDowntimeEntity>();
    public DbSet<ManufacturingMaintenanceWorkOrderEntity> MaintenanceWorkOrders => Set<ManufacturingMaintenanceWorkOrderEntity>();
    public DbSet<ManufacturingMaintenancePlanEntity> MaintenancePlans => Set<ManufacturingMaintenancePlanEntity>();
    public DbSet<ManufacturingMachineTelemetryEntity> MachineTelemetry => Set<ManufacturingMachineTelemetryEntity>();
    public DbSet<ManufacturingQualityInspectionEntity> QualityInspections => Set<ManufacturingQualityInspectionEntity>();
    public DbSet<ManufacturingQualitySampleEntity> QualitySamples => Set<ManufacturingQualitySampleEntity>();
    public DbSet<ManufacturingInspectionPlanVersionEntity> InspectionPlanVersions => Set<ManufacturingInspectionPlanVersionEntity>();
    public DbSet<ManufacturingQualityTestResultEntity> QualityTestResults => Set<ManufacturingQualityTestResultEntity>();
    public DbSet<ManufacturingProductSpecificationEntity> ProductSpecifications => Set<ManufacturingProductSpecificationEntity>();
    public DbSet<ManufacturingSupplierEntity> Suppliers => Set<ManufacturingSupplierEntity>();
    public DbSet<ManufacturingSupplierCertificateEntity> SupplierCertificates => Set<ManufacturingSupplierCertificateEntity>();
    public DbSet<ManufacturingSupplierMaterialApprovalEntity> SupplierMaterialApprovals => Set<ManufacturingSupplierMaterialApprovalEntity>();
    public DbSet<ManufacturingPurchaseOrderEntity> PurchaseOrders => Set<ManufacturingPurchaseOrderEntity>();
    public DbSet<ManufacturingPurchaseOrderLineEntity> PurchaseOrderLines => Set<ManufacturingPurchaseOrderLineEntity>();
    public DbSet<ManufacturingInboundReceiptEntity> InboundReceipts => Set<ManufacturingInboundReceiptEntity>();
    public DbSet<ManufacturingInventoryTransactionEntity> InventoryTransactions => Set<ManufacturingInventoryTransactionEntity>();
    public DbSet<ManufacturingLotReservationEntity> LotReservations => Set<ManufacturingLotReservationEntity>();
    public DbSet<ManufacturingProductionOrderEntity> ProductionOrders => Set<ManufacturingProductionOrderEntity>();
    public DbSet<ManufacturingProductionBatchEntity> ProductionBatches => Set<ManufacturingProductionBatchEntity>();
    public DbSet<ManufacturingProductionBatchCostEntity> ProductionBatchCosts => Set<ManufacturingProductionBatchCostEntity>();
    public DbSet<ManufacturingOperationExecutionEntity> OperationExecutions => Set<ManufacturingOperationExecutionEntity>();
    public DbSet<ManufacturingProductionBatchInputEntity> ProductionBatchInputs => Set<ManufacturingProductionBatchInputEntity>();
    public DbSet<ManufacturingLossReviewEntity> LossReviews => Set<ManufacturingLossReviewEntity>();
    public DbSet<ManufacturingDeviationEntity> Deviations => Set<ManufacturingDeviationEntity>();
    public DbSet<ManufacturingSalesForecastEntity> SalesForecasts => Set<ManufacturingSalesForecastEntity>();
    public DbSet<ManufacturingFacilityEntity> Facilities => Set<ManufacturingFacilityEntity>();
    public DbSet<ManufacturingWarehouseEntity> Warehouses => Set<ManufacturingWarehouseEntity>();
    public DbSet<ManufacturingStorageLocationEntity> StorageLocations => Set<ManufacturingStorageLocationEntity>();
    public DbSet<ManufacturingUomEntity> Uoms => Set<ManufacturingUomEntity>();
    public DbSet<ManufacturingUomConversionEntity> UomConversions => Set<ManufacturingUomConversionEntity>();
    public DbSet<ManufacturingMaterialEntity> Materials => Set<ManufacturingMaterialEntity>();
    public DbSet<ManufacturingProductEntity> Products => Set<ManufacturingProductEntity>();
    public DbSet<ManufacturingSupplierRfqEntity> SupplierRfqs => Set<ManufacturingSupplierRfqEntity>();
    public DbSet<ManufacturingSupplierQuotationEntity> SupplierQuotations => Set<ManufacturingSupplierQuotationEntity>();
    public DbSet<ManufacturingCapaEntity> Capas => Set<ManufacturingCapaEntity>();
    public DbSet<ManufacturingSupplierEvaluationEntity> SupplierEvaluations => Set<ManufacturingSupplierEvaluationEntity>();
    public DbSet<ManufacturingAuditEventEntity> AuditEvents => Set<ManufacturingAuditEventEntity>();
    public DbSet<ManufacturingMobileOperationReplayEntity> MobileOperationReplays => Set<ManufacturingMobileOperationReplayEntity>();
    public DbSet<ManufacturingOperationMeasurementEntity> OperationMeasurements => Set<ManufacturingOperationMeasurementEntity>();
    public DbSet<ManufacturingSalesActualEntity> SalesActuals => Set<ManufacturingSalesActualEntity>();
    public DbSet<ManufacturingMlFeatureSnapshotEntity> MlFeatureSnapshots => Set<ManufacturingMlFeatureSnapshotEntity>();
    public DbSet<ManufacturingSopArtifactEntity> SopArtifacts => Set<ManufacturingSopArtifactEntity>();
    public DbSet<ManufacturingSopArtifactAcknowledgmentEntity> SopArtifactAcknowledgments => Set<ManufacturingSopArtifactAcknowledgmentEntity>();
    public DbSet<ManufacturingBusinessSignatureEntity> BusinessSignatures => Set<ManufacturingBusinessSignatureEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ManufacturingMobileOperationReplayEntity>(entity =>
        {
            entity.ToTable("manufacturing_mobile_operation_replays");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(200).IsRequired();
            entity.Property(x => x.SubjectId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Method).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Path).HasMaxLength(500).IsRequired();
            entity.Property(x => x.OperationId).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.SubjectId, x.Method, x.Path, x.OperationId }).IsUnique();
        });

        modelBuilder.Entity<ManufacturingOperationMeasurementEntity>(entity =>
        {
            entity.ToTable("manufacturing_operation_measurements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.MeasurementType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Uom).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Value).HasPrecision(18, 6);
            entity.Property(x => x.RecordedBy).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantKey, x.ProductionBatchId, x.MeasuredAt });
            entity.HasIndex(x => new { x.TenantKey, x.OperationExecutionId, x.MeasurementType, x.Sequence }).IsUnique();
            entity.HasOne<ManufacturingProductionBatchEntity>().WithMany().HasForeignKey(x => x.ProductionBatchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ManufacturingSalesActualEntity>(entity =>
        {
            entity.ToTable("manufacturing_sales_actuals");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ProductSku).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.Uom).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Channel).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Region).HasMaxLength(100);
            entity.Property(x => x.Source).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Actor).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PeriodStart).HasColumnType("date");
            entity.Property(x => x.PeriodEnd).HasColumnType("date");
            entity.HasIndex(x => new { x.TenantKey, x.ProductSku, x.PeriodStart, x.Channel });
        });

        modelBuilder.Entity<ManufacturingMlFeatureSnapshotEntity>(entity =>
        {
            entity.ToTable("manufacturing_ml_feature_snapshots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DatasetKey).HasMaxLength(150).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.FeaturesJson).IsRequired();
            entity.Property(x => x.LabelJson);
            entity.Property(x => x.SourceEventIdsJson);
            entity.Property(x => x.Split).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.DatasetKey, x.AsOf });
            entity.HasIndex(x => new { x.TenantKey, x.DatasetKey, x.EntityType, x.EntityId, x.AsOf, x.SchemaVersion }).IsUnique();
        });

        modelBuilder.Entity<ManufacturingLotEntity>(entity =>
        {
            entity.ToTable("manufacturing_lots");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantKey, x.Sku, x.Disposition });
            entity.HasIndex(x => new { x.TenantKey, x.LotCode }).IsUnique();
            entity.Property(x => x.LotCode).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LotType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.OriginCountryCode).HasMaxLength(2);
            entity.Property(x => x.FacilityCode).HasMaxLength(100);
            entity.Property(x => x.StorageLocationCode).HasMaxLength(100);
            entity.Property(x => x.CertificateOfAnalysisReference).HasMaxLength(1000);
            entity.Property(x => x.SourceLotCode).HasMaxLength(100);
            entity.Property(x => x.QualityStatus).HasMaxLength(30).IsRequired();
            entity.Property(x => x.BestBefore).HasColumnType("date");
            entity.Property(x => x.ManufacturedOn).HasColumnType("date");
            entity.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.ToTable(t => t.HasCheckConstraint("CK_manufacturing_lots_lot_type", "\"LotType\" IN ('RawMaterial', 'WorkInProgress', 'FinishedGood', 'Packaging', 'Unspecified')"));
        });

        modelBuilder.Entity<ManufacturingLotStatusHistoryEntity>(entity =>
        {
            entity.ToTable("manufacturing_lot_status_history");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.FromDisposition).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ToDisposition).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Actor).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ReasonCode).HasMaxLength(100);
            entity.Property(x => x.EvidenceReference).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantKey, x.LotId, x.OccurredAt });
            entity.HasOne<ManufacturingLotEntity>().WithMany().HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ManufacturingEntityStatusHistoryEntity>(entity =>
        {
            entity.ToTable("manufacturing_entity_status_history");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EntityType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.FromStatus).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ToStatus).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Actor).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.EntityType, x.EntityId, x.OccurredAt });
        });

        modelBuilder.Entity<ManufacturingTransformationEntity>(entity =>
        {
            entity.ToTable("manufacturing_transformations");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OutputLotId);
            entity.Property(x => x.InputQuantity).HasPrecision(18, 3);
            entity.Property(x => x.OutputQuantity).HasPrecision(18, 3);
            entity.Property(x => x.YieldPercent).HasPrecision(8, 2);
            entity.Property(x => x.LossQuantity).HasPrecision(18, 3);
            entity.ToTable(t => t.HasCheckConstraint("CK_manufacturing_transformations_output_le_input", "\"OutputQuantity\" <= \"InputQuantity\""));
            entity.ToTable(t => t.HasCheckConstraint("CK_manufacturing_transformations_loss_non_negative", "\"LossQuantity\" >= 0"));
            entity.ToTable(t => t.HasCheckConstraint("CK_manufacturing_transformations_yield_range", "\"YieldPercent\" >= 0 AND \"YieldPercent\" <= 100"));
            entity.HasMany(x => x.Inputs).WithOne().HasForeignKey(x => x.TransformationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ManufacturingRecipeEntity>().WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ManufacturingMachineEntity>().WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ManufacturingTransformationInputEntity>(entity =>
        {
            entity.ToTable("manufacturing_transformation_inputs");
            entity.HasKey(x => new { x.TransformationId, x.LotId });
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.HasIndex(x => x.LotId);
        });

        modelBuilder.Entity<ManufacturingOutboxMessageEntity>(entity =>
        {
            entity.ToTable("manufacturing_outbox_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Content).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => new { x.Status, x.OccurredOn });
        });

        modelBuilder.Entity<ManufacturingEventReceiptEntity>(entity =>
        {
            entity.ToTable("manufacturing_event_receipts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(200).IsRequired();
            entity.Property(x => x.AggregateId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Content).IsRequired();
            entity.HasIndex(x => new { x.EventType, x.AggregateId }).IsUnique();
        });

        modelBuilder.Entity<ManufacturingRecipeEntity>(entity =>
        {
            entity.ToTable("manufacturing_recipes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ProductSku).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ProcessStep).HasMaxLength(100).IsRequired();
            entity.Property(x => x.OutputUom).HasMaxLength(30).IsRequired();
            entity.Property(x => x.TargetYieldPercent).HasPrecision(8, 2);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ApprovedBy).HasMaxLength(200);
            entity.HasOne<ManufacturingProductSpecificationEntity>().WithMany().HasForeignKey(x => x.ProductSpecificationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ProductSpecificationId);
            entity.HasIndex(x => new { x.TenantKey, x.ProductSku, x.Status });
            entity.HasIndex(x => new { x.TenantKey, x.ProductSku, x.Version }).IsUnique();
            entity.ToTable(t => t.HasCheckConstraint("CK_manufacturing_recipes_yield_range", "\"TargetYieldPercent\" > 0 AND \"TargetYieldPercent\" <= 100"));
            entity.HasMany(x => x.Components).WithOne().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ManufacturingRecipeComponentEntity>(entity =>
        {
            entity.ToTable("manufacturing_recipe_components");
            entity.HasKey(x => new { x.RecipeId, x.IngredientSku });
            entity.Property(x => x.IngredientSku).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.Uom).HasMaxLength(30).IsRequired();
            entity.ToTable(t => t.HasCheckConstraint("CK_manufacturing_recipe_components_quantity_positive", "\"Quantity\" > 0"));
        });

        modelBuilder.Entity<ManufacturingMachineEntity>(entity =>
        {
            entity.ToTable("manufacturing_machines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.Code }).IsUnique();
        });

        modelBuilder.Entity<ManufacturingMachineCalibrationEntity>(entity =>
        {
            entity.ToTable("manufacturing_machine_calibrations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CalibrationType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CertificateNumber).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Result).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Provider).HasMaxLength(200);
            entity.Property(x => x.EvidenceReference).HasMaxLength(1000);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.CreatedBy).HasMaxLength(200);
            entity.HasIndex(x => new { x.TenantKey, x.MachineId, x.CertificateNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantKey, x.MachineId, x.NextDueAt });
            entity.ToTable(t => t.HasCheckConstraint("CK_manufacturing_machine_calibration_dates", "\"NextDueAt\" > \"CalibratedAt\""));
            entity.HasOne<ManufacturingMachineEntity>().WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ManufacturingMachineDowntimeEntity>(entity =>
        {
            entity.ToTable("manufacturing_machine_downtimes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantKey, x.MachineId, x.Status });
            entity.HasIndex(x => x.StartedAt);
        });

        modelBuilder.Entity<ManufacturingMaintenanceWorkOrderEntity>(entity =>
        {
            entity.ToTable("manufacturing_maintenance_work_orders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.MaintenanceType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.AssignedTo).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.Technician).HasMaxLength(200);
            entity.Property(x => x.Evidence).HasMaxLength(4000);
            entity.HasIndex(x => new { x.TenantKey, x.MachineId, x.Status });
            entity.HasIndex(x => x.DueAt);
            entity.HasOne<ManufacturingMachineEntity>().WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ManufacturingMaintenancePlanEntity>(entity =>
        {
            entity.ToTable("manufacturing_maintenance_plans");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PlanCode).HasMaxLength(100).IsRequired();
            entity.Property(x => x.MaintenanceType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Checklist).HasMaxLength(4000);
            entity.Property(x => x.AssignedTo).HasMaxLength(200);
            entity.Property(x => x.CreatedBy).HasMaxLength(200);
            entity.HasIndex(x => new { x.TenantKey, x.MachineId, x.PlanCode }).IsUnique();
            entity.HasIndex(x => new { x.TenantKey, x.Active, x.NextDueAt });
            entity.ToTable(t => t.HasCheckConstraint("CK_manufacturing_maintenance_plan_frequency", "\"FrequencyDays\" > 0"));
            entity.HasOne<ManufacturingMachineEntity>().WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ManufacturingMachineTelemetryEntity>(entity =>
        {
            entity.ToTable("manufacturing_machine_telemetry");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(100).IsRequired();
            entity.Property(x => x.State).HasMaxLength(40);
            entity.Property(x => x.MeterName).HasMaxLength(100);
            entity.Property(x => x.MeterValue).HasPrecision(18, 3);
            entity.HasIndex(x => new { x.TenantKey, x.EventId }).IsUnique();
            entity.HasIndex(x => new { x.TenantKey, x.MachineId, x.ObservedAt });
            entity.HasOne<ManufacturingMachineEntity>().WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ManufacturingQualityInspectionEntity>(entity =>
        {
            entity.ToTable("manufacturing_quality_inspections");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Inspector).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.SpecificationReference).HasMaxLength(200);
            entity.Property(x => x.MoisturePercent).HasPrecision(8, 2);
            entity.HasIndex(x => new { x.TenantKey, x.LotId, x.InspectedAt });
            entity.HasOne<ManufacturingLotEntity>().WithMany().HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ManufacturingInspectionPlanVersionEntity>().WithMany().HasForeignKey(x => x.InspectionPlanVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_manufacturing_quality_moisture_range", "\"MoisturePercent\" >= 0 AND \"MoisturePercent\" <= 100"));
        });

        modelBuilder.Entity<ManufacturingInspectionPlanVersionEntity>(entity =>
        {
            entity.ToTable("manufacturing_inspection_plan_versions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PlanCode).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ProductSku).HasMaxLength(100).IsRequired();
            entity.Property(x => x.SamplingMethod).HasMaxLength(200).IsRequired();
            entity.Property(x => x.SamplingFrequency).HasMaxLength(100).IsRequired();
            entity.Property(x => x.AcceptanceCriteria).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ApprovedBy).HasMaxLength(200);
            entity.Property(x => x.CreatedBy).HasMaxLength(200);
            entity.HasIndex(x => new { x.TenantKey, x.PlanCode, x.Version }).IsUnique();
            entity.HasIndex(x => new { x.TenantKey, x.ProductSku, x.Status, x.EffectiveFrom });
            entity.ToTable(t => t.HasCheckConstraint("CK_manufacturing_inspection_plan_version_dates", "\"EffectiveTo\" IS NULL OR \"EffectiveFrom\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\""));
        });

        modelBuilder.Entity<ManufacturingQualitySampleEntity>(entity =>
        {
            entity.ToTable("manufacturing_quality_samples");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.SampleCode).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CollectedBy).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Disposition).HasMaxLength(30).IsRequired();
            entity.Property(x => x.DispositionReason).HasMaxLength(2000);
            entity.Property(x => x.DisposedBy).HasMaxLength(200);
            entity.Property(x => x.Location).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantKey, x.InspectionId, x.SampleCode }).IsUnique();
            entity.HasIndex(x => new { x.TenantKey, x.Disposition, x.CollectedAt });
            entity.HasOne<ManufacturingQualityInspectionEntity>().WithMany().HasForeignKey(x => x.InspectionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ManufacturingLotEntity>().WithMany().HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ManufacturingSupplierEntity>(entity =>
        {
            entity.ToTable("manufacturing_suppliers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.LegalName).HasMaxLength(300).IsRequired();
            entity.Property(x => x.TaxIdentificationNumber).HasMaxLength(100);
            entity.Property(x => x.ContactName).HasMaxLength(200);
            entity.Property(x => x.ContactEmail).HasMaxLength(320);
            entity.Property(x => x.ContactPhone).HasMaxLength(50);
            entity.Property(x => x.CountryCode).HasMaxLength(2);
            entity.Property(x => x.Address).HasMaxLength(1000);
            entity.Property(x => x.RiskLevel).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ApprovalStatus).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ApprovedBy).HasMaxLength(200);
            entity.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.Code }).IsUnique();
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_manufacturing_supplier_risk_level", "\"RiskLevel\" IN ('Low', 'Standard', 'High', 'Critical')");
                t.HasCheckConstraint("CK_manufacturing_supplier_approval_status", "\"ApprovalStatus\" IN ('Draft', 'PendingApproval', 'Approved', 'Suspended', 'Rejected')");
            });
        });

        modelBuilder.Entity<ManufacturingQualityTestResultEntity>(entity =>
        {
            entity.ToTable("manufacturing_quality_test_results");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TestCode).HasMaxLength(100).IsRequired();
            entity.Property(x => x.TestName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Uom).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Result).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Method).HasMaxLength(200);
            entity.Property(x => x.EvidenceReference).HasMaxLength(1000);
            entity.Property(x => x.MeasuredValue).HasPrecision(18, 6);
            entity.Property(x => x.LowerLimit).HasPrecision(18, 6);
            entity.Property(x => x.UpperLimit).HasPrecision(18, 6);
            entity.HasIndex(x => new { x.QualityInspectionId, x.TestCode }).IsUnique();
            entity.HasOne<ManufacturingQualityInspectionEntity>().WithMany().HasForeignKey(x => x.QualityInspectionId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t => t.HasCheckConstraint("CK_manufacturing_quality_test_result_status", "\"Result\" IN ('Pass', 'Fail', 'NotApplicable')"));
        });

        modelBuilder.Entity<ManufacturingPurchaseOrderEntity>(entity =>
        {
            entity.ToTable("manufacturing_purchase_orders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.OrderNumber).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.OrderNumber }).IsUnique();
            entity.HasOne<ManufacturingSupplierEntity>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ManufacturingPurchaseOrderLineEntity>(entity =>
        {
            entity.ToTable("manufacturing_purchase_order_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MaterialSku).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Uom).HasMaxLength(30).IsRequired();
            entity.Property(x => x.OrderedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.ReceivedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 4);
            entity.ToTable(t => t.HasCheckConstraint("CK_manufacturing_po_lines_quantity_positive", "\"OrderedQuantity\" > 0 AND \"ReceivedQuantity\" >= 0 AND \"ReceivedQuantity\" <= \"OrderedQuantity\""));
        });

        modelBuilder.Entity<ManufacturingInboundReceiptEntity>(entity =>
        {
            entity.ToTable("manufacturing_inbound_receipts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ReceiptNumber).HasMaxLength(100).IsRequired();
            entity.Property(x => x.SupplierLotCode).HasMaxLength(100).IsRequired();
            entity.Property(x => x.FacilityId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.StorageLocationCode).HasMaxLength(100);
            entity.Property(x => x.DeliveryNoteNumber).HasMaxLength(100);
            entity.Property(x => x.CarrierName).HasMaxLength(200);
            entity.Property(x => x.VehicleReference).HasMaxLength(100);
            entity.Property(x => x.CertificateOfAnalysisReference).HasMaxLength(1000);
            entity.Property(x => x.ReceivedBy).HasMaxLength(200);
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.TemperatureOnReceiptC).HasPrecision(6, 2);
            entity.Property(x => x.AcceptedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.RejectedQuantity).HasPrecision(18, 3);
            entity.ToTable(t => t.HasCheckConstraint("CK_manufacturing_inbound_receipts_quantity_balance", "\"AcceptedQuantity\" + \"RejectedQuantity\" = \"Quantity\""));
            entity.HasIndex(x => new { x.TenantKey, x.ReceiptNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantKey, x.SupplierId, x.SupplierLotCode }).IsUnique();
            entity.HasOne<ManufacturingPurchaseOrderEntity>().WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ManufacturingPurchaseOrderLineEntity>().WithMany().HasForeignKey(x => x.PurchaseOrderLineId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ManufacturingLotEntity>().WithMany().HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ManufacturingSupplierEntity>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ManufacturingInventoryTransactionEntity>(entity =>
        {
            entity.ToTable("manufacturing_inventory_transactions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.TransactionType).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Uom).HasMaxLength(30).IsRequired();
            entity.Property(x => x.FacilityId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.StockStatus).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.HasIndex(x => new { x.TenantKey, x.LotId, x.OccurredAt });
            entity.HasIndex(x => new { x.TransactionType, x.CorrelationId, x.LotId }).IsUnique();
            entity.HasOne<ManufacturingLotEntity>().WithMany().HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ManufacturingFacilityEntity>(entity =>
        {
            entity.ToTable("manufacturing_facilities");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.Code }).IsUnique();
        });

        modelBuilder.Entity<ManufacturingWarehouseEntity>(entity =>
        {
            entity.ToTable("manufacturing_warehouses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.Code }).IsUnique();
            entity.HasOne<ManufacturingFacilityEntity>().WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ManufacturingStorageLocationEntity>(entity =>
        {
            entity.ToTable("manufacturing_storage_locations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.WarehouseId, x.Code }).IsUnique();
            entity.HasOne<ManufacturingWarehouseEntity>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ManufacturingUomEntity>(entity =>
        {
            entity.ToTable("manufacturing_uoms"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired(); entity.Property(x => x.Name).HasMaxLength(100).IsRequired(); entity.Property(x => x.Dimension).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
        });
        modelBuilder.Entity<ManufacturingUomConversionEntity>(entity =>
        {
            entity.ToTable("manufacturing_uom_conversions"); entity.HasKey(x => x.Id);
            entity.Property(x => x.FromCode).HasMaxLength(30).IsRequired(); entity.Property(x => x.ToCode).HasMaxLength(30).IsRequired(); entity.Property(x => x.Factor).HasPrecision(18, 8);
            entity.HasIndex(x => new { x.FromCode, x.ToCode }).IsUnique();
            entity.ToTable(t => t.HasCheckConstraint("CK_manufacturing_uom_conversion_factor_positive", "\"Factor\" > 0"));
        });
        modelBuilder.Entity<ManufacturingMaterialEntity>(entity =>
        {
            entity.ToTable("manufacturing_materials"); entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired(); entity.Property(x => x.Sku).HasMaxLength(100).IsRequired(); entity.Property(x => x.Name).HasMaxLength(200).IsRequired(); entity.Property(x => x.BaseUomCode).HasMaxLength(30).IsRequired(); entity.Property(x => x.MaterialType).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.Sku }).IsUnique(); entity.HasOne<ManufacturingUomEntity>().WithMany().HasForeignKey(x => x.BaseUomCode).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ManufacturingProductEntity>(entity =>
        {
            entity.ToTable("manufacturing_products"); entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired(); entity.Property(x => x.Sku).HasMaxLength(100).IsRequired(); entity.Property(x => x.Name).HasMaxLength(200).IsRequired(); entity.Property(x => x.BaseUomCode).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.Sku }).IsUnique(); entity.HasOne<ManufacturingUomEntity>().WithMany().HasForeignKey(x => x.BaseUomCode).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ManufacturingSupplierCertificateEntity>(entity =>
        {
            entity.ToTable("manufacturing_supplier_certificates"); entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CertificateType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CertificateNumber).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Issuer).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.EvidenceReference).HasMaxLength(1000);
            entity.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.SupplierId, x.CertificateNumber }).IsUnique();
            entity.HasOne<ManufacturingSupplierEntity>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t => t.HasCheckConstraint("CK_manufacturing_supplier_certificate_dates", "\"ExpiresAt\" > \"IssuedAt\""));
        });
        modelBuilder.Entity<ManufacturingSupplierMaterialApprovalEntity>(entity =>
        {
            entity.ToTable("manufacturing_supplier_material_approvals"); entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired(); entity.Property(x => x.MaterialSku).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ApprovedUom).HasMaxLength(30).IsRequired(); entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000); entity.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.SupplierId, x.MaterialSku }).IsUnique();
            entity.HasOne<ManufacturingSupplierEntity>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t => t.HasCheckConstraint("CK_manufacturing_supplier_material_approval_dates", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\""));
        });
        modelBuilder.Entity<ManufacturingSupplierRfqEntity>(entity =>
        {
            entity.ToTable("manufacturing_supplier_rfqs"); entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired(); entity.Property(x => x.RfqNumber).HasMaxLength(100).IsRequired(); entity.Property(x => x.MaterialSku).HasMaxLength(100).IsRequired(); entity.Property(x => x.Uom).HasMaxLength(30).IsRequired(); entity.Property(x => x.Quantity).HasPrecision(18, 3); entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.RfqNumber }).IsUnique();
        });
        modelBuilder.Entity<ManufacturingSupplierQuotationEntity>(entity =>
        {
            entity.ToTable("manufacturing_supplier_quotations"); entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired(); entity.Property(x => x.UnitPrice).HasPrecision(18, 4); entity.Property(x => x.Currency).HasMaxLength(10).IsRequired(); entity.Property(x => x.Status).HasMaxLength(30).IsRequired(); entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantKey, x.SupplierRfqId, x.SupplierId }).IsUnique();
            entity.HasOne<ManufacturingSupplierRfqEntity>().WithMany().HasForeignKey(x => x.SupplierRfqId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ManufacturingSupplierEntity>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ManufacturingCapaEntity>(entity =>
        {
            entity.ToTable("manufacturing_capas"); entity.HasKey(x => x.Id); entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired(); entity.Property(x => x.Title).HasMaxLength(300).IsRequired(); entity.Property(x => x.ProblemDescription).HasMaxLength(4000).IsRequired(); entity.Property(x => x.RootCause).HasMaxLength(4000).IsRequired(); entity.Property(x => x.CorrectiveAction).HasMaxLength(4000).IsRequired(); entity.Property(x => x.PreventiveAction).HasMaxLength(4000).IsRequired(); entity.Property(x => x.Owner).HasMaxLength(200).IsRequired(); entity.Property(x => x.Status).HasMaxLength(30).IsRequired(); entity.HasIndex(x => new { x.TenantKey, x.Status });
            entity.HasOne<ManufacturingDeviationEntity>().WithMany().HasForeignKey(x => x.DeviationId).OnDelete(DeleteBehavior.SetNull); entity.HasOne<ManufacturingSupplierEntity>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<ManufacturingSupplierEvaluationEntity>(entity =>
        {
            entity.ToTable("manufacturing_supplier_evaluations"); entity.HasKey(x => x.Id); entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired(); entity.Property(x => x.QualityNotes).HasMaxLength(2000); entity.Property(x => x.DeliveryNotes).HasMaxLength(2000); entity.Property(x => x.Notes).HasMaxLength(2000); entity.Property(x => x.EvaluatedBy).HasMaxLength(200).IsRequired(); entity.HasIndex(x => new { x.TenantKey, x.SupplierId, x.EvaluatedAt }); entity.HasOne<ManufacturingSupplierEntity>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict); entity.ToTable(t => t.HasCheckConstraint("CK_manufacturing_supplier_evaluations_score", "\"Score\" >= 1 AND \"Score\" <= 5"));
        });
        modelBuilder.Entity<ManufacturingAuditEventEntity>(entity =>
        {
            entity.ToTable("manufacturing_audit_events"); entity.HasKey(x => x.Id); entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired(); entity.Property(x => x.EntityType).HasMaxLength(100).IsRequired(); entity.Property(x => x.Action).HasMaxLength(100).IsRequired(); entity.Property(x => x.Actor).HasMaxLength(200).IsRequired(); entity.Property(x => x.Details).HasMaxLength(4000).IsRequired(); entity.HasIndex(x => new { x.TenantKey, x.EntityType, x.EntityId, x.OccurredAt });
        });

        modelBuilder.Entity<ManufacturingProductSpecificationEntity>(entity =>
        {
            entity.ToTable("manufacturing_product_specifications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ProductSku).HasMaxLength(100).IsRequired();
            entity.Property(x => x.TargetMoisturePercent).HasPrecision(5, 2);
            entity.Property(x => x.Packaging).HasMaxLength(500).IsRequired();
            entity.Property(x => x.QcSpec).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ApprovedBy).HasMaxLength(200);
            entity.HasIndex(x => new { x.TenantKey, x.ProductSku, x.Status });
        });

        modelBuilder.Entity<ManufacturingSalesForecastEntity>(entity =>
        {
            entity.ToTable("manufacturing_sales_forecasts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ProductSku).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.Uom).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Actor).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PeriodStart).HasColumnType("date");
            entity.Property(x => x.PeriodEnd).HasColumnType("date");
            entity.HasIndex(x => new { x.TenantKey, x.ProductSku, x.PeriodStart, x.PeriodEnd, x.Version }).IsUnique();
        });

        modelBuilder.Entity<ManufacturingLotReservationEntity>(entity =>
        {
            entity.ToTable("manufacturing_lot_reservations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ReferenceType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Uom).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.HasIndex(x => new { x.TenantKey, x.ReferenceType, x.ReferenceId, x.LotId }).IsUnique();
            entity.HasIndex(x => new { x.TenantKey, x.LotId, x.Status });
            entity.HasOne<ManufacturingLotEntity>().WithMany().HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ManufacturingProductionOrderEntity>(entity =>
        {
            entity.ToTable("manufacturing_production_orders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.OrderNumber).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ProductSku).HasMaxLength(100).IsRequired();
            entity.Property(x => x.OutputUom).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.TargetQuantity).HasPrecision(18, 3);
            entity.HasIndex(x => new { x.TenantKey, x.OrderNumber }).IsUnique();
            entity.HasOne<ManufacturingRecipeEntity>().WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ManufacturingProductionBatchEntity>(entity =>
        {
            entity.ToTable("manufacturing_production_batches");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.BatchNumber).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.PlannedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.ActualOutputQuantity).HasPrecision(18, 3);
            entity.Property(x => x.OutputLotId).HasColumnType("uuid");
            entity.HasIndex(x => new { x.TenantKey, x.BatchNumber }).IsUnique();
            entity.HasOne<ManufacturingProductionOrderEntity>().WithMany().HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ManufacturingMachineEntity>().WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ManufacturingProductionBatchCostEntity>(entity =>
        {
            entity.ToTable("manufacturing_production_batch_costs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.MaterialCost).HasPrecision(18, 2);
            entity.Property(x => x.LaborCost).HasPrecision(18, 2);
            entity.Property(x => x.OverheadCost).HasPrecision(18, 2);
            entity.Property(x => x.LossCost).HasPrecision(18, 2);
            entity.Property(x => x.TotalCost).HasPrecision(18, 2);
            entity.Property(x => x.CostPerOutputUnit).HasPrecision(18, 4);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.CalculatedBy).HasMaxLength(200);
            entity.HasIndex(x => new { x.TenantKey, x.ProductionBatchId }).IsUnique();
            entity.HasOne<ManufacturingProductionBatchEntity>().WithMany().HasForeignKey(x => x.ProductionBatchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ManufacturingOperationExecutionEntity>(entity =>
        {
            entity.ToTable("manufacturing_operation_executions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProcessStep).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Operator).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.QcStatus).HasMaxLength(30).IsRequired();
            entity.Property(x => x.InputQuantity).HasPrecision(18, 3);
            entity.Property(x => x.OutputQuantity).HasPrecision(18, 3);
            entity.Property(x => x.LossQuantity).HasPrecision(18, 3);
            entity.HasIndex(x => new { x.ProductionBatchId, x.Sequence }).IsUnique();
            entity.HasOne<ManufacturingProductionBatchEntity>().WithMany().HasForeignKey(x => x.ProductionBatchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ManufacturingLossReviewEntity>(entity =>
        {
            entity.ToTable("manufacturing_loss_reviews");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Decision).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Reviewer).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantKey, x.OperationExecutionId }).IsUnique();
            entity.HasOne<ManufacturingProductionBatchEntity>().WithMany().HasForeignKey(x => x.ProductionBatchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ManufacturingOperationExecutionEntity>().WithMany().HasForeignKey(x => x.OperationExecutionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ManufacturingDeviationEntity>(entity =>
        {
            entity.ToTable("manufacturing_deviations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Type).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Impact).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.RequestedBy).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ApprovedBy).HasMaxLength(200);
            entity.Property(x => x.ResolutionNotes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantKey, x.ProductionBatchId, x.Status });
            entity.HasOne<ManufacturingProductionBatchEntity>().WithMany().HasForeignKey(x => x.ProductionBatchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ManufacturingProductionBatchInputEntity>(entity =>
        {
            entity.ToTable("manufacturing_production_batch_inputs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.HasIndex(x => new { x.ProductionBatchId, x.LotId }).IsUnique();
            entity.HasOne<ManufacturingProductionBatchEntity>().WithMany().HasForeignKey(x => x.ProductionBatchId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ManufacturingLotEntity>().WithMany().HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ManufacturingLotReservationEntity>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ManufacturingSopArtifactEntity>(entity =>
        {
            entity.ToTable("manufacturing_sop_artifacts"); entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired(); entity.Property(x => x.ArtifactKey).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired(); entity.Property(x => x.Content).IsRequired(); entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired(); entity.Property(x => x.Checksum).HasMaxLength(64).IsRequired(); entity.Property(x => x.ApprovedBy).HasMaxLength(200); entity.Property(x => x.CreatedBy).HasMaxLength(200);
            entity.HasIndex(x => new { x.TenantKey, x.ArtifactKey, x.Version }).IsUnique(); entity.HasIndex(x => new { x.TenantKey, x.Status });
        });

        modelBuilder.Entity<ManufacturingBusinessSignatureEntity>(entity =>
        {
            entity.ToTable("manufacturing_business_signatures"); entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired(); entity.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(100).IsRequired(); entity.Property(x => x.Reason).HasMaxLength(2000).IsRequired(); entity.Property(x => x.EvidenceReference).HasMaxLength(1000);
            entity.Property(x => x.Actor).HasMaxLength(200).IsRequired(); entity.Property(x => x.SignatureMethod).HasMaxLength(40).IsRequired(); entity.Property(x => x.SignatureHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.EntityType, x.EntityId, x.Action, x.Actor }).IsUnique(); entity.HasIndex(x => new { x.TenantKey, x.SignedAt });
        });

        modelBuilder.Entity<ManufacturingSopArtifactAcknowledgmentEntity>(entity =>
        {
            entity.ToTable("manufacturing_sop_artifact_acknowledgments"); entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired(); entity.Property(x => x.Actor).HasMaxLength(200).IsRequired(); entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantKey, x.SopArtifactId, x.Actor }).IsUnique();
            entity.HasOne<ManufacturingSopArtifactEntity>().WithMany().HasForeignKey(x => x.SopArtifactId).OnDelete(DeleteBehavior.Cascade);
        });

        HisHopeDataConventions.Apply(
            modelBuilder,
            typeof(ManufacturingLotEntity), typeof(ManufacturingRecipeEntity),
            typeof(ManufacturingMachineEntity), typeof(ManufacturingProductionOrderEntity),
            typeof(ManufacturingProductionBatchEntity), typeof(ManufacturingOperationExecutionEntity),
            typeof(ManufacturingQualityInspectionEntity), typeof(ManufacturingSupplierEntity),
            typeof(ManufacturingPurchaseOrderEntity), typeof(ManufacturingProductEntity),
            typeof(ManufacturingMaterialEntity));
    }
}

public sealed class ManufacturingLotEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string Sku { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = "";
    public string Disposition { get; set; } = ManufacturingStatusCodes.Released;
    public DateOnly? BestBefore { get; set; }
    public string LotCode { get; set; } = "";
    public string LotType { get; set; } = "Unspecified";
    public string? OriginCountryCode { get; set; }
    public DateOnly? ManufacturedOn { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
    public string? FacilityCode { get; set; }
    public string? StorageLocationCode { get; set; }
    public string? CertificateOfAnalysisReference { get; set; }
    public string? SourceLotCode { get; set; }
    public string QualityStatus { get; set; } = ManufacturingStatusCodes.Pending;
    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ManufacturingLotStatusHistoryEntity
{
    public Guid Id { get; set; }
    public Guid LotId { get; set; }
    public string TenantKey { get; set; } = "";
    public string FromDisposition { get; set; } = "";
    public string ToDisposition { get; set; } = "";
    public string Actor { get; set; } = "system";
    public string? ReasonCode { get; set; }
    public string? EvidenceReference { get; set; }
    public Guid CorrelationId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class ManufacturingTransformationEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string ProcessStep { get; set; } = "";
    public Guid OutputLotId { get; set; }
    public Guid? RecipeId { get; set; }
    public Guid? MachineId { get; set; }
    public decimal InputQuantity { get; set; }
    public decimal OutputQuantity { get; set; }
    public decimal YieldPercent { get; set; }
    public decimal LossQuantity { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<ManufacturingTransformationInputEntity> Inputs { get; set; } = [];
}

public sealed class ManufacturingTransformationInputEntity
{
    public Guid TransformationId { get; set; }
    public Guid LotId { get; set; }
    public decimal Quantity { get; set; }
}

public sealed partial class PostgresManufacturingStore(IDbContextFactory<ManufacturingDbContext> dbFactory) :
    IManufacturingProductionStore, IManufacturingMaintenanceStore, IManufacturingDashboardStore,
    IManufacturingQualityWorkflowStore,
    IManufacturingRecipeWorkflowStore, IManufacturingPlanningWorkflowStore
{
    public void Initialize()
    {
        using var db = dbFactory.CreateDbContext();
        if (db.Lots.Any())
        {
            if (!db.InventoryTransactions.Any())
            {
                var existingLots = db.Lots.AsNoTracking().ToList();
                db.InventoryTransactions.AddRange(existingLots.Select(lot => new ManufacturingInventoryTransactionEntity
                {
                    Id = Guid.NewGuid(), TenantKey = lot.TenantKey, LotId = lot.Id, TransactionType = "OpeningBalance",
                    Quantity = lot.Quantity, Uom = lot.Uom, FacilityId = "default", StockStatus = lot.Disposition,
                    CorrelationId = lot.Id, OccurredAt = lot.CreatedAt
                }));
                db.SaveChanges();
            }
            return;
        }

        var input = new ManufacturingLotEntity
        {
            Id = Guid.NewGuid(), TenantKey = "customer-factory-x", Sku = "RM-MANGO-001",
            Quantity = 600, Uom = "kg", Disposition = ManufacturingStatusCodes.Released, LotCode = "LOT-LEGACY-MANGO-001",
            LotType = "RawMaterial", QualityStatus = "Passed", CreatedBy = "system", CreatedAt = DateTimeOffset.UtcNow
        };
        var output = new ManufacturingLotEntity
        {
            Id = Guid.NewGuid(), TenantKey = input.TenantKey, Sku = "FX-MANGO-SOFT",
            Quantity = 320, Uom = "kg", Disposition = ManufacturingStatusCodes.Released, LotCode = "LOT-LEGACY-FG-001",
            LotType = "FinishedGood", QualityStatus = "Passed", CreatedBy = "system", CreatedAt = DateTimeOffset.UtcNow
        };
        db.Lots.AddRange(input, output);
        var seededTransformation = new ManufacturingTransformationEntity
        {
            Id = Guid.NewGuid(), TenantKey = input.TenantKey, ProcessStep = "drying", OutputLotId = output.Id,
            InputQuantity = 400, OutputQuantity = output.Quantity, YieldPercent = 80, LossQuantity = 80,
            CreatedAt = DateTimeOffset.UtcNow,
            Inputs = [new ManufacturingTransformationInputEntity { LotId = input.Id, Quantity = 400 }]
        };
        db.Transformations.Add(seededTransformation);
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            Type = "Manufacturing.TransformationCompleted.v1",
            Content = JsonSerializer.Serialize(new { eventId = seededTransformation.Id, schemaVersion = 1, occurredAt = DateTimeOffset.UtcNow, correlationId = seededTransformation.Id, facilityId = (string?)null, transformationId = seededTransformation.Id, tenantKey = input.TenantKey, outputLotId = output.Id, outputSku = output.Sku, outputQuantity = output.Quantity, yieldPercent = seededTransformation.YieldPercent, lossQuantity = seededTransformation.LossQuantity }),
            OccurredOn = DateTime.UtcNow,
            Status = ManufacturingStatusCodes.Pending
        });
        db.SaveChanges();
    }


    public AvailabilityDto GetAvailability(string tenantKey, string sku)
    {
        using var db = dbFactory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var releasedLots = db.Lots.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.Sku == sku && x.Disposition == ManufacturingStatusCodes.Released)
            .ToList();
        var lotIds = releasedLots.Select(x => x.Id).ToArray();
        var releasedQuantity = releasedLots.Sum(x => x.Quantity);
        var reservedQuantity = db.LotReservations.AsNoTracking()
            .Where(x => lotIds.Contains(x.LotId) && x.Status == "Reserved" && (x.ExpiresAt == null || x.ExpiresAt > now))
            .Sum(x => (decimal?)x.Quantity) ?? 0;
        var uom = db.Lots.Where(x => x.TenantKey == tenantKey && x.Sku == sku).Select(x => x.Uom).FirstOrDefault() ?? "kg";
        return new AvailabilityDto(tenantKey, sku, releasedQuantity, reservedQuantity, Math.Max(0, releasedQuantity - reservedQuantity), uom, now);
    }

    public async Task<IReadOnlyList<LotDto>> GetLotsAsync(string? tenantKey, string? sku, string? disposition, int limit, int page = 1, CancellationToken cancellationToken = default)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.Lots.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(tenantKey)) query = query.Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(sku)) query = query.Where(x => x.Sku == sku);
        if (!string.IsNullOrWhiteSpace(disposition)) query = query.Where(x => x.Disposition == disposition);

        return (await query.TagUseCase("Manufacturing.Traceability.GetLots")
            .OrderByDescending(x => x.CreatedAt)
            .ApplyPage(page, limit)
            .ToListAsync(cancellationToken))
            .Select(ToDto)
            .ToList();
    }

    public Task<IReadOnlyList<LotDto>> GetLotsAsync(string? sku, string? disposition, int limit, int page = 1, CancellationToken cancellationToken = default) =>
        GetLotsAsync(HisHopeTenantScope.Current ?? throw new InvalidOperationException("Tenant context is required."), sku, disposition, limit, page, cancellationToken);

    public IReadOnlyList<LotStatusHistoryDto> GetLotStatusHistory(Guid lotId, string tenantKey, int limit, int page = 1)
    {
        using var db = dbFactory.CreateDbContext();
        return db.LotStatusHistory.AsNoTracking()
            .Where(x => x.LotId == lotId && x.TenantKey == tenantKey)
            .TagUseCase("Manufacturing.Traceability.GetLotStatusHistory")
            .OrderByDescending(x => x.OccurredAt).ApplyPage(page, limit, 100)
            .Select(x => new LotStatusHistoryDto(x.Id, x.LotId, x.TenantKey, x.FromDisposition, x.ToDisposition,
                x.Actor, x.ReasonCode, x.EvidenceReference, x.CorrelationId, x.OccurredAt))
            .ToList();
    }

    public IReadOnlyList<TransformationSummaryDto> GetTransformationSummaries(string? processStep, int limit, int page = 1) =>
        GetTransformationSummaries(HisHopeTenantScope.Current ?? throw new InvalidOperationException("Tenant context is required."), processStep, limit, page);

    public IReadOnlyList<TransformationSummaryDto> GetTransformationSummaries(string? tenantKey, string? processStep, int limit, int page = 1)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.Transformations.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(tenantKey)) query = query.Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(processStep)) query = query.Where(x => x.ProcessStep == processStep);

        return query.TagUseCase("Manufacturing.Production.GetTransformationSummaries")
            .OrderByDescending(x => x.CreatedAt)
            .ApplyPage(page, limit)
            .Select(x => new TransformationSummaryDto(
                x.Id, x.TenantKey, x.ProcessStep, x.RecipeId, x.MachineId, x.OutputLotId,
                x.InputQuantity, x.OutputQuantity, x.YieldPercent, x.LossQuantity, x.CreatedAt))
            .ToList();
    }


    private static LotDto ToDto(ManufacturingLotEntity x) =>
        new(x.Id, x.TenantKey, x.Sku, x.Quantity, x.Uom, x.Disposition, x.BestBefore, x.CreatedAt,
            x.LotCode, x.LotType, x.OriginCountryCode, x.ManufacturedOn, x.ReceivedAt, x.FacilityCode,
            x.StorageLocationCode, x.CertificateOfAnalysisReference, x.SourceLotCode, x.QualityStatus, x.CreatedBy, x.UpdatedAt);
    private static TransformationDto ToDto(ManufacturingTransformationEntity x, IReadOnlyList<TransformationInput> inputs, LotDto output) =>
        new(x.Id, x.TenantKey, x.ProcessStep, x.RecipeId, x.MachineId, inputs, output, x.InputQuantity, x.YieldPercent, x.LossQuantity, x.CreatedAt);
    private static RecipeDto ToDto(ManufacturingRecipeEntity x) =>
        new(x.Id, x.TenantKey, x.ProductSku, x.Version, x.ProcessStep, x.OutputUom, x.TargetYieldPercent, x.Active, x.Status,
            x.EffectiveFrom, x.EffectiveTo, x.ApprovedBy, x.ApprovedAt,
            x.Components.Select(c => new RecipeComponentDto(c.IngredientSku, c.Quantity, c.Uom)).ToList(), x.CreatedAt, x.ProductSpecificationId);
    private static MachineDto ToDto(ManufacturingMachineEntity x) =>
        new(x.Id, x.TenantKey, x.Code, x.Name, x.Status, x.LastMaintenanceAt, x.NextMaintenanceAt, x.Active, x.CreatedAt);
    private static MachineCalibrationDto ToDto(ManufacturingMachineCalibrationEntity x) =>
        new(x.Id, x.MachineId, x.TenantKey, x.CalibrationType, x.CertificateNumber, x.CalibratedAt, x.NextDueAt, x.Result, x.Provider, x.EvidenceReference, x.Notes, x.CreatedBy, x.CreatedAt);
    private static MachineTelemetryDto ToDto(ManufacturingMachineTelemetryEntity x) =>
        new(x.Id, x.EventId, x.MachineId, x.TenantKey, x.Source, x.State, x.MeterName, x.MeterValue, x.Sequence, x.ObservedAt, x.ReceivedAt);
    private static DowntimeDto ToDto(ManufacturingMachineDowntimeEntity x) =>
        new(x.Id, x.MachineId, x.TenantKey, x.Reason, x.Status, x.ProductionBatchId, x.OperationExecutionId, x.StartedAt, x.EndedAt, x.Notes, x.CreatedAt);
    private static MaintenanceWorkOrderDto ToDto(ManufacturingMaintenanceWorkOrderEntity x) =>
        new(x.Id, x.MachineId, x.TenantKey, x.Status, x.MaintenanceType, x.DueAt, x.AssignedTo, x.Notes, x.Technician, x.CompletedAt, x.Evidence, x.CreatedAt);
    private static MaintenancePlanDto ToDto(ManufacturingMaintenancePlanEntity x) =>
        new(x.Id, x.MachineId, x.TenantKey, x.PlanCode, x.MaintenanceType, x.FrequencyDays, x.NextDueAt, x.Checklist, x.AssignedTo, x.Active, x.LastGeneratedAt, x.CreatedBy, x.CreatedAt);
    private static QualityInspectionDto ToDto(ManufacturingQualityInspectionEntity x, IReadOnlyList<ManufacturingQualityTestResultEntity>? results = null) =>
        new(x.Id, x.LotId, x.TenantKey, x.Status, x.MoisturePercent, x.Inspector, x.Notes, x.InspectedAt,
            results?.Select(result => new QualityTestResultDto(result.Id, result.TestCode, result.TestName, result.MeasuredValue, result.Uom, result.Result, result.LowerLimit, result.UpperLimit, result.Method, result.EvidenceReference)).ToList(),
            x.SpecificationReference, x.InspectionPlanVersionId);
    private static InspectionPlanVersionDto ToDto(ManufacturingInspectionPlanVersionEntity x) =>
        new(x.Id, x.TenantKey, x.PlanCode, x.ProductSku, x.Version, x.SamplingMethod, x.SamplingFrequency, x.AcceptanceCriteria, x.Status, x.EffectiveFrom, x.EffectiveTo, x.ApprovedBy, x.ApprovedAt, x.CreatedBy, x.CreatedAt);
    private static QualitySampleDto ToDto(ManufacturingQualitySampleEntity x) =>
        new(x.Id, x.InspectionId, x.LotId, x.TenantKey, x.SampleCode, x.CollectedBy, x.CollectedAt, x.Disposition, x.DispositionReason, x.DisposedBy, x.DisposedAt, x.Location, x.Notes, x.CreatedAt);
}

public sealed class ManufacturingOutboxMessageEntity
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime OccurredOn { get; set; }
    public DateTime? ProcessedOn { get; set; }
    public string Status { get; set; } = ManufacturingStatusCodes.Pending;
    public string? Error { get; set; }
    public int RetryCount { get; set; }
}

public sealed class ManufacturingEventReceiptEntity
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = "";
    public string AggregateId { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
}

public sealed class ManufacturingRecipeEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string ProductSku { get; set; } = "";
    public int Version { get; set; }
    public string ProcessStep { get; set; } = "";
    public string OutputUom { get; set; } = "";
    public decimal TargetYieldPercent { get; set; }
    public bool Active { get; set; }
    public string Status { get; set; } = ManufacturingStatusCodes.Approved;
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? ProductSpecificationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<ManufacturingRecipeComponentEntity> Components { get; set; } = [];
}

public sealed class ManufacturingRecipeComponentEntity
{
    public Guid RecipeId { get; set; }
    public string IngredientSku { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = "";
}

public sealed class ManufacturingMachineEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Status { get; set; } = "Available";
    public DateTimeOffset? LastMaintenanceAt { get; set; }
    public DateTimeOffset? NextMaintenanceAt { get; set; }
    public bool Active { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ManufacturingMachineCalibrationEntity
{
    public Guid Id { get; set; }
    public Guid MachineId { get; set; }
    public string TenantKey { get; set; } = "";
    public string CalibrationType { get; set; } = "";
    public string CertificateNumber { get; set; } = "";
    public DateTimeOffset CalibratedAt { get; set; }
    public DateTimeOffset NextDueAt { get; set; }
    public string Result { get; set; } = "Pass";
    public string? Provider { get; set; }
    public string? EvidenceReference { get; set; }
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ManufacturingMachineTelemetryEntity
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid MachineId { get; set; }
    public string TenantKey { get; set; } = "";
    public string Source { get; set; } = "";
    public string? State { get; set; }
    public string? MeterName { get; set; }
    public decimal? MeterValue { get; set; }
    public long? Sequence { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}

public sealed class ManufacturingLossReviewEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public Guid ProductionBatchId { get; set; }
    public Guid OperationExecutionId { get; set; }
    public string Decision { get; set; } = ManufacturingStatusCodes.Approved;
    public string Reviewer { get; set; } = "";
    public string? Notes { get; set; }
    public DateTimeOffset ReviewedAt { get; set; }
}

public sealed class ManufacturingMachineDowntimeEntity
{
    public Guid Id { get; set; }
    public Guid MachineId { get; set; }
    public string TenantKey { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "Open";
    public Guid? ProductionBatchId { get; set; }
    public Guid? OperationExecutionId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ManufacturingMaintenanceWorkOrderEntity
{
    public Guid Id { get; set; }
    public Guid MachineId { get; set; }
    public string TenantKey { get; set; } = "";
    public string Status { get; set; } = "Open";
    public string MaintenanceType { get; set; } = "Preventive";
    public DateTimeOffset DueAt { get; set; }
    public string? AssignedTo { get; set; }
    public string? Notes { get; set; }
    public string? Technician { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Evidence { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ManufacturingMaintenancePlanEntity
{
    public Guid Id { get; set; }
    public Guid MachineId { get; set; }
    public string TenantKey { get; set; } = "";
    public string PlanCode { get; set; } = "";
    public string MaintenanceType { get; set; } = "Preventive";
    public int FrequencyDays { get; set; }
    public DateTimeOffset NextDueAt { get; set; }
    public string? Checklist { get; set; }
    public string? AssignedTo { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset? LastGeneratedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ManufacturingQualitySampleEntity
{
    public Guid Id { get; set; }
    public Guid InspectionId { get; set; }
    public Guid LotId { get; set; }
    public string TenantKey { get; set; } = "";
    public string SampleCode { get; set; } = "";
    public string CollectedBy { get; set; } = "";
    public DateTimeOffset CollectedAt { get; set; }
    public string Disposition { get; set; } = ManufacturingStatusCodes.Pending;
    public string? DispositionReason { get; set; }
    public string? DisposedBy { get; set; }
    public DateTimeOffset? DisposedAt { get; set; }
    public string? Location { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ManufacturingInspectionPlanVersionEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string PlanCode { get; set; } = "";
    public string ProductSku { get; set; } = "";
    public int Version { get; set; }
    public string SamplingMethod { get; set; } = "";
    public string SamplingFrequency { get; set; } = "";
    public string AcceptanceCriteria { get; set; } = "";
    public string Status { get; set; } = ManufacturingStatusCodes.Draft;
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ManufacturingQualityInspectionEntity
{
    public Guid Id { get; set; }
    public Guid LotId { get; set; }
    public Guid? InspectionPlanVersionId { get; set; }
    public string TenantKey { get; set; } = "";
    public string Status { get; set; } = ManufacturingStatusCodes.Pending;
    public decimal MoisturePercent { get; set; }
    public string Inspector { get; set; } = "";
    public string? Notes { get; set; }
    public string? SpecificationReference { get; set; }
    public DateTimeOffset InspectedAt { get; set; }
}

public sealed class ManufacturingQualityTestResultEntity
{
    public Guid Id { get; set; }
    public Guid QualityInspectionId { get; set; }
    public string TestCode { get; set; } = "";
    public string TestName { get; set; } = "";
    public decimal MeasuredValue { get; set; }
    public string Uom { get; set; } = "";
    public string Result { get; set; } = "Pass";
    public decimal? LowerLimit { get; set; }
    public decimal? UpperLimit { get; set; }
    public string? Method { get; set; }
    public string? EvidenceReference { get; set; }
}

public sealed class ManufacturingProductSpecificationEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string ProductSku { get; set; } = "";
    public decimal TargetMoisturePercent { get; set; }
    public string Packaging { get; set; } = "";
    public int ShelfLifeDays { get; set; }
    public string QcSpec { get; set; } = "";
    public string Status { get; set; } = ManufacturingStatusCodes.Draft;
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ManufacturingSalesForecastEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string ProductSku { get; set; } = "";
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = "";
    public string Source { get; set; } = "Sales";
    public string Actor { get; set; } = "system";
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ManufacturingInventoryTransactionEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public Guid LotId { get; set; }
    public string TransactionType { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = "";
    public string FacilityId { get; set; } = "default";
    public string StockStatus { get; set; } = "";
    public Guid CorrelationId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
