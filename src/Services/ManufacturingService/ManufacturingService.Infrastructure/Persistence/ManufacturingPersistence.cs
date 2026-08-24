using Microsoft.EntityFrameworkCore;
using His.Hope.ManufacturingService.Application.Ports;
using System.Text.Json;
using System.Data;

public sealed class ManufacturingDbContext(DbContextOptions<ManufacturingDbContext> options) : DbContext(options)
{
    public DbSet<ManufacturingLotEntity> Lots => Set<ManufacturingLotEntity>();
    public DbSet<ManufacturingTransformationEntity> Transformations => Set<ManufacturingTransformationEntity>();
    public DbSet<ManufacturingTransformationInputEntity> TransformationInputs => Set<ManufacturingTransformationInputEntity>();
    public DbSet<ManufacturingOutboxMessageEntity> OutboxMessages => Set<ManufacturingOutboxMessageEntity>();
    public DbSet<ManufacturingEventReceiptEntity> EventReceipts => Set<ManufacturingEventReceiptEntity>();
    public DbSet<ManufacturingRecipeEntity> Recipes => Set<ManufacturingRecipeEntity>();
    public DbSet<ManufacturingRecipeComponentEntity> RecipeComponents => Set<ManufacturingRecipeComponentEntity>();
    public DbSet<ManufacturingMachineEntity> Machines => Set<ManufacturingMachineEntity>();
    public DbSet<ManufacturingMachineDowntimeEntity> MachineDowntimes => Set<ManufacturingMachineDowntimeEntity>();
    public DbSet<ManufacturingMaintenanceWorkOrderEntity> MaintenanceWorkOrders => Set<ManufacturingMaintenanceWorkOrderEntity>();
    public DbSet<ManufacturingMachineTelemetryEntity> MachineTelemetry => Set<ManufacturingMachineTelemetryEntity>();
    public DbSet<ManufacturingQualityInspectionEntity> QualityInspections => Set<ManufacturingQualityInspectionEntity>();
    public DbSet<ManufacturingProductSpecificationEntity> ProductSpecifications => Set<ManufacturingProductSpecificationEntity>();
    public DbSet<ManufacturingSupplierEntity> Suppliers => Set<ManufacturingSupplierEntity>();
    public DbSet<ManufacturingPurchaseOrderEntity> PurchaseOrders => Set<ManufacturingPurchaseOrderEntity>();
    public DbSet<ManufacturingPurchaseOrderLineEntity> PurchaseOrderLines => Set<ManufacturingPurchaseOrderLineEntity>();
    public DbSet<ManufacturingInboundReceiptEntity> InboundReceipts => Set<ManufacturingInboundReceiptEntity>();
    public DbSet<ManufacturingInventoryTransactionEntity> InventoryTransactions => Set<ManufacturingInventoryTransactionEntity>();
    public DbSet<ManufacturingLotReservationEntity> LotReservations => Set<ManufacturingLotReservationEntity>();
    public DbSet<ManufacturingProductionOrderEntity> ProductionOrders => Set<ManufacturingProductionOrderEntity>();
    public DbSet<ManufacturingProductionBatchEntity> ProductionBatches => Set<ManufacturingProductionBatchEntity>();
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ManufacturingLotEntity>(entity =>
        {
            entity.ToTable("manufacturing_lots");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantKey, x.Sku, x.Disposition });
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
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
            entity.Property(x => x.MoisturePercent).HasPrecision(8, 2);
            entity.HasIndex(x => new { x.TenantKey, x.LotId, x.InspectedAt });
            entity.HasOne<ManufacturingLotEntity>().WithMany().HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_manufacturing_quality_moisture_range", "\"MoisturePercent\" >= 0 AND \"MoisturePercent\" <= 100"));
        });

        modelBuilder.Entity<ManufacturingSupplierEntity>(entity =>
        {
            entity.ToTable("manufacturing_suppliers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.TenantKey, x.Code }).IsUnique();
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
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
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
    }
}

public sealed class ManufacturingLotEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string Sku { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = "";
    public string Disposition { get; set; } = "Released";
    public DateOnly? BestBefore { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
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

public sealed partial class PostgresManufacturingStore(IDbContextFactory<ManufacturingDbContext> dbFactory) : IManufacturingProductionStore, IManufacturingMaintenanceStore, IManufacturingDashboardStore, IManufacturingLegacyStore
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
            Quantity = 600, Uom = "kg", Disposition = "Released", CreatedAt = DateTimeOffset.UtcNow
        };
        var output = new ManufacturingLotEntity
        {
            Id = Guid.NewGuid(), TenantKey = input.TenantKey, Sku = "FX-MANGO-SOFT",
            Quantity = 320, Uom = "kg", Disposition = "Released", CreatedAt = DateTimeOffset.UtcNow
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
            Status = "Pending"
        });
        db.SaveChanges();
    }

    public bool LotExists(Guid lotId)
    {
        using var db = dbFactory.CreateDbContext();
        return db.Lots.Any(x => x.Id == lotId);
    }

    public bool LotBelongsToTenant(Guid lotId, string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        return db.Lots.Any(x => x.Id == lotId && x.TenantKey == tenantKey);
    }

    public LotDto CreateLot(CreateLotRequest request)
    {
        var entity = new ManufacturingLotEntity
        {
            Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), Sku = request.Sku.Trim(),
            Quantity = request.Quantity, Uom = request.Uom.Trim(), Disposition = request.Disposition.Trim(),
            BestBefore = request.BestBefore, CreatedAt = DateTimeOffset.UtcNow
        };
        using var db = dbFactory.CreateDbContext();
        db.Lots.Add(entity);
        db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
        {
            Id = Guid.NewGuid(), TenantKey = entity.TenantKey, LotId = entity.Id, TransactionType = "Receipt",
            Quantity = entity.Quantity, Uom = entity.Uom, FacilityId = "default", StockStatus = entity.Disposition,
            CorrelationId = entity.Id, OccurredAt = entity.CreatedAt
        });
        db.SaveChanges();
        return ToDto(entity);
    }

    public (TransformationDto? Transformation, string? Error) CreateTransformation(CreateTransformationRequest request)
    {
        if (request.Inputs.GroupBy(x => x.LotId).Any(x => x.Count() > 1)) return (null, "duplicate_input_lot");
        using var db = dbFactory.CreateDbContext();
        ManufacturingRecipeEntity? recipe = null;
        ManufacturingMachineEntity? machine = null;
        if (request.RecipeId.HasValue)
        {
            recipe = db.Recipes.SingleOrDefault(x => x.Id == request.RecipeId.Value);
            if (recipe is null) return (null, "recipe_not_found");
            if (!recipe.Active || !recipe.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase)) return (null, "recipe_unavailable");
            if (!recipe.TenantKey.Equals(request.TenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "recipe_tenant_mismatch");
            if (!recipe.ProductSku.Equals(request.OutputSku, StringComparison.OrdinalIgnoreCase)) return (null, "recipe_product_mismatch");
            if (!recipe.ProcessStep.Equals(request.ProcessStep, StringComparison.OrdinalIgnoreCase)) return (null, "recipe_process_step_mismatch");
        }
        if (request.MachineId.HasValue)
        {
            machine = db.Machines.SingleOrDefault(x => x.Id == request.MachineId.Value);
            if (machine is null) return (null, "machine_not_found");
            if (!machine.Active) return (null, "machine_inactive");
            if (!machine.TenantKey.Equals(request.TenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "machine_tenant_mismatch");
        }
        var inputIds = request.Inputs.Select(x => x.LotId).ToArray();
        var lots = db.Lots.Where(x => inputIds.Contains(x.Id)).ToDictionary(x => x.Id);
        var inputs = new List<(ManufacturingLotEntity Lot, decimal Quantity, ManufacturingLotReservationEntity? Reservation)>();
        var reservationNow = DateTimeOffset.UtcNow;
        foreach (var input in request.Inputs)
        {
            if (!lots.TryGetValue(input.LotId, out var lot)) return (null, "input_lot_not_found");
            if (!lot.TenantKey.Equals(request.TenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_mismatch");
            if (!lot.Disposition.Equals("Released", StringComparison.OrdinalIgnoreCase)) return (null, "input_lot_not_released");
            ManufacturingLotReservationEntity? reservation = null;
            if (input.ReservationId.HasValue)
            {
                reservation = db.LotReservations.SingleOrDefault(x => x.Id == input.ReservationId.Value);
                if (reservation is null) return (null, "reservation_not_found");
                if (!reservation.TenantKey.Equals(request.TenantKey, StringComparison.OrdinalIgnoreCase) || reservation.LotId != lot.Id) return (null, "reservation_mismatch");
                if (reservation.Status != "Reserved" || input.Quantity > reservation.Quantity) return (null, "reservation_unavailable");
            }
            var reservedByOther = db.LotReservations.Where(x => x.LotId == lot.Id && x.Status == "Reserved" && (x.ExpiresAt == null || x.ExpiresAt > reservationNow) && (!input.ReservationId.HasValue || x.Id != input.ReservationId.Value)).Sum(x => (decimal?)x.Quantity) ?? 0;
            if (input.Quantity <= 0 || input.Quantity + reservedByOther > lot.Quantity) return (null, "input_quantity_exceeds_available");
            inputs.Add((lot, input.Quantity, reservation));
        }

        var inputQuantity = inputs.Sum(x => x.Quantity);
        if (request.OutputQuantity > inputQuantity)
            return (null, "output_quantity_exceeds_input");

        foreach (var (lot, quantity, reservation) in inputs)
        {
            lot.Quantity -= quantity;
            if (reservation is not null) reservation.Status = "Consumed";
        }
        var output = new ManufacturingLotEntity
        {
            Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), Sku = request.OutputSku.Trim(),
            Quantity = request.OutputQuantity, Uom = request.OutputUom.Trim(), Disposition = "Released", CreatedAt = DateTimeOffset.UtcNow
        };
        var transformation = new ManufacturingTransformationEntity
        {
            Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), ProcessStep = request.ProcessStep.Trim(),
            OutputLotId = output.Id, RecipeId = recipe?.Id, MachineId = machine?.Id, InputQuantity = inputQuantity, OutputQuantity = output.Quantity,
            YieldPercent = decimal.Round(output.Quantity / inputQuantity * 100, 2), LossQuantity = inputQuantity - output.Quantity,
            CreatedAt = DateTimeOffset.UtcNow,
            Inputs = inputs.Select(x => new ManufacturingTransformationInputEntity { LotId = x.Lot.Id, Quantity = x.Quantity }).ToList()
        };
        db.Lots.Add(output);
        db.Transformations.Add(transformation);
        foreach (var (lot, quantity, _) in inputs)
        {
            db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
            {
                Id = Guid.NewGuid(), TenantKey = transformation.TenantKey, LotId = lot.Id, TransactionType = "Issue",
                Quantity = quantity, Uom = lot.Uom, FacilityId = "default", StockStatus = lot.Disposition,
                CorrelationId = transformation.Id, OccurredAt = transformation.CreatedAt
            });
        }
        db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
        {
            Id = Guid.NewGuid(), TenantKey = transformation.TenantKey, LotId = output.Id, TransactionType = "Produce",
            Quantity = output.Quantity, Uom = output.Uom, FacilityId = "default", StockStatus = output.Disposition,
            CorrelationId = transformation.Id, OccurredAt = transformation.CreatedAt
        });
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            Type = "Manufacturing.TransformationCompleted.v1",
            Content = JsonSerializer.Serialize(new
            {
                eventId = transformation.Id,
                schemaVersion = 1,
                occurredAt = DateTimeOffset.UtcNow,
                correlationId = transformation.Id,
                facilityId = (string?)null,
                transformationId = transformation.Id,
                recipeId = transformation.RecipeId,
                machineId = transformation.MachineId,
                tenantKey = transformation.TenantKey,
                processStep = transformation.ProcessStep,
                outputLotId = output.Id,
                outputSku = output.Sku,
                inputQuantity,
                outputQuantity = output.Quantity,
                yieldPercent = transformation.YieldPercent,
                lossQuantity = transformation.LossQuantity
            }),
            OccurredOn = DateTime.UtcNow,
            Status = "Pending"
        });
        db.SaveChanges();
        return (ToDto(transformation, inputs.Select(x => new TransformationInput(x.Lot.Id, x.Quantity, x.Reservation?.Id)).ToList(), ToDto(output)), null);
    }

    public GenealogyDto GetGenealogy(Guid lotId, bool upstream, string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var lot = db.Lots.Single(x => x.Id == lotId && x.TenantKey == tenantKey);
        var allTransformations = db.Transformations.AsNoTracking().Include(x => x.Inputs).Where(x => x.TenantKey == tenantKey).ToList();
        var linkedLotIds = new HashSet<Guid> { lotId };
        var frontier = new HashSet<Guid> { lotId };
        var visitedTransformations = new HashSet<Guid>();
        var relations = new List<LotRelationDto>();
        for (var depth = 0; depth < 32 && frontier.Count > 0 && linkedLotIds.Count < 2000; depth++)
        {
            var next = new HashSet<Guid>();
            foreach (var transformation in allTransformations)
            {
                if (visitedTransformations.Contains(transformation.Id)) continue;
                var touchesFrontier = upstream
                    ? frontier.Contains(transformation.OutputLotId)
                    : transformation.Inputs.Any(input => frontier.Contains(input.LotId));
                if (!touchesFrontier) continue;
                visitedTransformations.Add(transformation.Id);
                foreach (var input in transformation.Inputs)
                {
                    relations.Add(new LotRelationDto(transformation.Id, input.LotId, "", "input", input.Quantity));
                    if (upstream && linkedLotIds.Add(input.LotId)) next.Add(input.LotId);
                }
                relations.Add(new LotRelationDto(transformation.Id, transformation.OutputLotId, "", "output", transformation.OutputQuantity));
                if (!upstream && linkedLotIds.Add(transformation.OutputLotId)) next.Add(transformation.OutputLotId);
            }
            frontier = next;
        }
        var linkedLots = db.Lots.AsNoTracking().Where(x => linkedLotIds.Contains(x.Id) && x.TenantKey == tenantKey).ToDictionary(x => x.Id);
        relations = relations.Where(x => linkedLots.ContainsKey(x.LotId)).Select(x => x with { Sku = linkedLots[x.LotId].Sku }).ToList();
        return new GenealogyDto(ToDto(lot), relations);
    }

    public AvailabilityDto GetAvailability(string tenantKey, string sku)
    {
        using var db = dbFactory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var releasedLots = db.Lots.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.Sku == sku && x.Disposition == "Released")
            .ToList();
        var lotIds = releasedLots.Select(x => x.Id).ToArray();
        var releasedQuantity = releasedLots.Sum(x => x.Quantity);
        var reservedQuantity = db.LotReservations.AsNoTracking()
            .Where(x => lotIds.Contains(x.LotId) && x.Status == "Reserved" && (x.ExpiresAt == null || x.ExpiresAt > now))
            .Sum(x => (decimal?)x.Quantity) ?? 0;
        var uom = db.Lots.Where(x => x.TenantKey == tenantKey && x.Sku == sku).Select(x => x.Uom).FirstOrDefault() ?? "kg";
        return new AvailabilityDto(tenantKey, sku, releasedQuantity, reservedQuantity, Math.Max(0, releasedQuantity - reservedQuantity), uom, now);
    }

    public IReadOnlyList<LotDto> GetLots(string? tenantKey, string? sku, string? disposition, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.Lots.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(tenantKey)) query = query.Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(sku)) query = query.Where(x => x.Sku == sku);
        if (!string.IsNullOrWhiteSpace(disposition)) query = query.Where(x => x.Disposition == disposition);

        return query.OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .AsEnumerable()
            .Select(ToDto)
            .ToList();
    }

    public (LotDto? Lot, string? Error) SetLotDisposition(Guid lotId, string disposition, string tenantKey)
    {
        var normalized = disposition.Trim();
        if (!AllowedLotDispositions.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            return (null, "invalid_disposition");
        using var db = dbFactory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
        var lot = db.Lots.SingleOrDefault(x => x.Id == lotId);
        if (lot is null) return (null, "lot_not_found");
        if (!lot.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_scope_denied");
        if (lot.Disposition.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            return (ToDto(lot), null);

        var now = DateTimeOffset.UtcNow;
        var held = normalized is "Quarantined" or "Rejected" or "Hold";
        var activeReservations = held
            ? db.LotReservations.Where(x => x.TenantKey == lot.TenantKey && x.LotId == lot.Id && x.Status == "Reserved").ToList()
            : [];
        lot.Disposition = normalized;
        var dispositionEventId = Guid.NewGuid();
        db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
        {
            Id = Guid.NewGuid(), TenantKey = lot.TenantKey, LotId = lot.Id,
            TransactionType = normalized.Equals("Released", StringComparison.OrdinalIgnoreCase) ? "Release" : "Hold",
            Quantity = lot.Quantity, Uom = lot.Uom, FacilityId = "default", StockStatus = lot.Disposition,
            CorrelationId = dispositionEventId, OccurredAt = now
        });
        foreach (var reservation in activeReservations)
        {
            reservation.Status = "Cancelled";
            db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
            {
                Id = Guid.NewGuid(), TenantKey = lot.TenantKey, LotId = lot.Id, TransactionType = "Unreserve",
                Quantity = reservation.Quantity, Uom = reservation.Uom, FacilityId = "default", StockStatus = lot.Disposition,
                CorrelationId = reservation.Id, OccurredAt = now
            });
            db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
            {
                Id = Guid.NewGuid(), Type = "Manufacturing.InventoryReservationCancelled.v1",
                Content = JsonSerializer.Serialize(new
                {
                    eventId = reservation.Id, schemaVersion = 1, occurredAt = now, correlationId = reservation.Id,
                    facilityId = "default", reservationId = reservation.Id, lotId = lot.Id, tenantKey = lot.TenantKey,
                    quantity = reservation.Quantity, reason = "lot_disposition_changed", disposition = lot.Disposition
                }),
                OccurredOn = now.UtcDateTime, Status = "Pending"
            });
        }
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = dispositionEventId, Type = "Manufacturing.LotDispositionChanged.v1",
            Content = JsonSerializer.Serialize(new
            {
                eventId = dispositionEventId, schemaVersion = 1, occurredAt = now, correlationId = dispositionEventId,
                facilityId = (string?)null, lotId = lot.Id, tenantKey = lot.TenantKey, disposition = lot.Disposition,
                cancelledReservationCount = activeReservations.Count
            }),
            OccurredOn = now.UtcDateTime, Status = "Pending"
        });
        db.SaveChanges();
        transaction.Commit();
        return (ToDto(lot), null);
    }

    private static readonly string[] AllowedLotDispositions = ["Released", "Quarantined", "Rejected", "Hold", "Consumed"];

    public (QualityInspectionDto? Inspection, string? Error) CreateQualityInspection(CreateQualityInspectionRequest request)
    {
        var normalizedStatus = request.Status.Trim();
        if (!AllowedInspectionStatuses.Contains(normalizedStatus, StringComparer.OrdinalIgnoreCase)) return (null, "invalid_inspection_status");
        if (request.MoisturePercent is < 0 or > 100) return (null, "invalid_moisture_percent");
        using var db = dbFactory.CreateDbContext();
        var lot = db.Lots.SingleOrDefault(x => x.Id == request.LotId);
        if (lot is null) return (null, "lot_not_found");
        if (!lot.TenantKey.Equals(request.TenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_mismatch");
        var entity = db.QualityInspections
            .Where(x => x.LotId == lot.Id && x.TenantKey == lot.TenantKey && x.Status == "Pending")
            .OrderByDescending(x => x.InspectedAt)
            .FirstOrDefault();
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
        var dispositionChanged = false;
        var dispositionEventId = Guid.NewGuid();
        if (normalizedStatus.Equals("Pass", StringComparison.OrdinalIgnoreCase) &&
            (lot.Disposition.Equals("Quarantined", StringComparison.OrdinalIgnoreCase) || lot.Disposition.Equals("Hold", StringComparison.OrdinalIgnoreCase)))
        {
            lot.Disposition = "Released";
            dispositionChanged = true;
        }
        else if (normalizedStatus.Equals("Fail", StringComparison.OrdinalIgnoreCase) &&
                 lot.Disposition.Equals("Released", StringComparison.OrdinalIgnoreCase))
        {
            lot.Disposition = "Quarantined";
            dispositionChanged = true;
        }
        if (dispositionChanged)
        {
            db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
            {
                Id = Guid.NewGuid(), TenantKey = lot.TenantKey, LotId = lot.Id,
                TransactionType = lot.Disposition.Equals("Released", StringComparison.OrdinalIgnoreCase) ? "Release" : "Hold",
                Quantity = lot.Quantity, Uom = lot.Uom, FacilityId = "default", StockStatus = lot.Disposition,
                CorrelationId = entity.Id, OccurredAt = entity.InspectedAt
            });
            db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
            {
                Id = Guid.NewGuid(), Type = "Manufacturing.LotDispositionChanged.v1",
                Content = JsonSerializer.Serialize(new { eventId = dispositionEventId, schemaVersion = 1, occurredAt = entity.InspectedAt, correlationId = entity.Id, facilityId = "default", lotId = lot.Id, tenantKey = lot.TenantKey, disposition = lot.Disposition, reason = "quality_inspection" }),
                OccurredOn = entity.InspectedAt.UtcDateTime, Status = "Pending"
            });
        }
        var inspectionEventId = Guid.NewGuid();
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = inspectionEventId, Type = "Manufacturing.QualityInspectionRecorded.v1",
            Content = JsonSerializer.Serialize(new { eventId = inspectionEventId, schemaVersion = 1, occurredAt = DateTimeOffset.UtcNow, correlationId = inspectionEventId, facilityId = (string?)null, inspectionId = entity.Id, lotId = entity.LotId, tenantKey = entity.TenantKey, status = entity.Status, moisturePercent = entity.MoisturePercent }),
            OccurredOn = DateTime.UtcNow, Status = "Pending"
        });
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    public IReadOnlyList<QualityInspectionDto> GetQualityInspections(Guid lotId, string? tenantKey, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.QualityInspections.AsNoTracking().Where(x => x.LotId == lotId);
        if (!string.IsNullOrWhiteSpace(tenantKey)) query = query.Where(x => x.TenantKey == tenantKey);
        return query.OrderByDescending(x => x.InspectedAt).Take(Math.Clamp(limit, 1, 100)).AsEnumerable().Select(ToDto).ToList();
    }

    private static readonly string[] AllowedInspectionStatuses = ["Pass", "Fail", "Pending"];

    public IReadOnlyList<TransformationSummaryDto> GetTransformationSummaries(string? tenantKey, string? processStep, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.Transformations.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(tenantKey)) query = query.Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(processStep)) query = query.Where(x => x.ProcessStep == processStep);

        return query.OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(x => new TransformationSummaryDto(
                x.Id, x.TenantKey, x.ProcessStep, x.RecipeId, x.MachineId, x.OutputLotId,
                x.InputQuantity, x.OutputQuantity, x.YieldPercent, x.LossQuantity, x.CreatedAt))
            .ToList();
    }

    public RecipeDto CreateRecipe(CreateRecipeRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        if (db.Recipes.Any(x => x.TenantKey == request.TenantKey && x.ProductSku == request.ProductSku && x.Version == request.Version))
            throw new InvalidOperationException("recipe_version_exists");
        var status = string.IsNullOrWhiteSpace(request.Status) ? "Approved" : request.Status.Trim();
        if (status is not ("Draft" or "Submitted" or "Approved" or "Retired")) throw new InvalidOperationException("invalid_recipe_status");
        ManufacturingProductSpecificationEntity? specification = null;
        if (request.ProductSpecificationId.HasValue)
        {
            specification = db.ProductSpecifications.SingleOrDefault(x => x.Id == request.ProductSpecificationId.Value);
            if (specification is null || !specification.TenantKey.Equals(request.TenantKey.Trim(), StringComparison.OrdinalIgnoreCase) ||
                !specification.ProductSku.Equals(request.ProductSku.Trim(), StringComparison.OrdinalIgnoreCase) || specification.Status != "Approved")
                throw new InvalidOperationException("invalid_product_specification");
        }
        var entity = new ManufacturingRecipeEntity
        {
            Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), ProductSku = request.ProductSku.Trim(),
            Version = request.Version, ProcessStep = request.ProcessStep.Trim(), OutputUom = request.OutputUom.Trim(),
            TargetYieldPercent = request.TargetYieldPercent, Active = request.Active && status == "Approved", Status = status,
            EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo,
            ProductSpecificationId = specification?.Id,
            ApprovedAt = status == "Approved" ? DateTimeOffset.UtcNow : null, CreatedAt = DateTimeOffset.UtcNow,
            Components = request.Components!.Select(x => new ManufacturingRecipeComponentEntity
            {
                IngredientSku = x.IngredientSku.Trim(), Quantity = x.Quantity, Uom = x.Uom.Trim()
            }).ToList()
        };
        db.Recipes.Add(entity);
        db.SaveChanges();
        return ToDto(entity);
    }

    public IReadOnlyList<RecipeDto> GetRecipes(string? tenantKey, string? productSku, bool? active, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.Recipes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(tenantKey)) query = query.Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(productSku)) query = query.Where(x => x.ProductSku == productSku);
        if (active.HasValue) query = query.Where(x => x.Active == active.Value);
        return query.Include(x => x.Components).OrderByDescending(x => x.ProductSku).ThenByDescending(x => x.Version)
            .Take(Math.Clamp(limit, 1, 200)).AsEnumerable().Select(ToDto).ToList();
    }

    public (RecipeDto? Recipe, string? Error) ChangeRecipeLifecycle(Guid recipeId, string tenantKey, string targetStatus, RecipeLifecycleRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = db.Recipes.Include(x => x.Components).SingleOrDefault(x => x.Id == recipeId);
        if (entity is null) return (null, "recipe_not_found");
        if (!entity.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_scope_denied");
        if (string.IsNullOrWhiteSpace(request.Actor)) return (null, "invalid_recipe_actor");
        var valid = (entity.Status, targetStatus) switch
        {
            ("Draft", "Submitted") => true,
            ("Submitted", "Approved") => true,
            ("Approved", "Retired") => true,
            _ => false
        };
        if (!valid) return (null, "invalid_recipe_transition");
        entity.Status = targetStatus;
        entity.Active = targetStatus == "Approved";
        if (targetStatus == "Approved")
        {
            entity.ApprovedBy = request.Actor.Trim();
            entity.ApprovedAt = DateTimeOffset.UtcNow;
            entity.EffectiveFrom = request.EffectiveFrom ?? entity.EffectiveFrom ?? DateTimeOffset.UtcNow;
            entity.EffectiveTo = request.EffectiveTo ?? entity.EffectiveTo;
        }
        var eventId = Guid.NewGuid();
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = eventId, Type = $"Manufacturing.Recipe{targetStatus}v1",
            Content = JsonSerializer.Serialize(new { eventId, schemaVersion = 1, occurredAt = DateTimeOffset.UtcNow, correlationId = entity.Id, recipeId = entity.Id, tenantKey = entity.TenantKey, status = entity.Status, actor = request.Actor }),
            OccurredOn = DateTime.UtcNow, Status = "Pending"
        });
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    public MachineDto CreateMachine(CreateMachineRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        if (db.Machines.Any(x => x.TenantKey == request.TenantKey && x.Code == request.Code))
            throw new InvalidOperationException("machine_code_exists");
        var entity = new ManufacturingMachineEntity
        {
            Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), Code = request.Code.Trim(), Name = request.Name.Trim(),
            Status = request.Status.Trim(), LastMaintenanceAt = request.LastMaintenanceAt, NextMaintenanceAt = request.NextMaintenanceAt,
            Active = request.Active, CreatedAt = DateTimeOffset.UtcNow
        };
        db.Machines.Add(entity);
        db.SaveChanges();
        return ToDto(entity);
    }

    public IReadOnlyList<MachineDto> GetMachines(string? tenantKey, string? status, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.Machines.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(tenantKey)) query = query.Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return query.OrderBy(x => x.Code).Take(Math.Clamp(limit, 1, 200)).AsEnumerable().Select(ToDto).ToList();
    }

    public (MachineTelemetryDto? Telemetry, string? Error, bool Duplicate) RecordMachineTelemetry(Guid machineId, RecordMachineTelemetryRequest request, string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var machine = db.Machines.SingleOrDefault(x => x.Id == machineId);
        if (machine is null) return (null, "machine_not_found", false);
        if (!machine.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_scope_denied", false);
        if (request.EventId == Guid.Empty || request.ObservedAt == default || request.ObservedAt > DateTimeOffset.UtcNow.AddMinutes(5) ||
            string.IsNullOrWhiteSpace(request.Source) || (string.IsNullOrWhiteSpace(request.State) && string.IsNullOrWhiteSpace(request.MeterName)))
            return (null, "invalid_machine_telemetry", false);

        var existing = db.MachineTelemetry.SingleOrDefault(x => x.TenantKey == tenantKey && x.EventId == request.EventId);
        if (existing is not null) return (ToDto(existing), null, true);

        var entity = new ManufacturingMachineTelemetryEntity
        {
            Id = Guid.NewGuid(), EventId = request.EventId, MachineId = machineId, TenantKey = tenantKey,
            Source = request.Source.Trim(), State = string.IsNullOrWhiteSpace(request.State) ? null : request.State.Trim(),
            MeterName = string.IsNullOrWhiteSpace(request.MeterName) ? null : request.MeterName.Trim(), MeterValue = request.MeterValue,
            Sequence = request.Sequence, ObservedAt = request.ObservedAt, ReceivedAt = DateTimeOffset.UtcNow
        };
        db.MachineTelemetry.Add(entity);
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = "Manufacturing.MachineTelemetryRecorded.v1",
            Content = JsonSerializer.Serialize(new { eventId = entity.EventId, schemaVersion = 1, occurredAt = entity.ObservedAt, receivedAt = entity.ReceivedAt, correlationId = entity.Id, facilityId = "default", machineId, tenantKey, source = entity.Source, state = entity.State, meterName = entity.MeterName, meterValue = entity.MeterValue, sequence = entity.Sequence }),
            OccurredOn = entity.ReceivedAt.UtcDateTime, Status = "Pending", RetryCount = 0
        });
        db.SaveChanges();
        return (ToDto(entity), null, false);
    }

    public IReadOnlyList<MachineTelemetryDto> GetMachineTelemetry(Guid machineId, string tenantKey, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        return db.MachineTelemetry.AsNoTracking()
            .Where(x => x.MachineId == machineId && x.TenantKey == tenantKey)
            .OrderByDescending(x => x.ObservedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .AsEnumerable().Select(ToDto).ToList();
    }

    public (MachineDto? Machine, string? Error) RecordMaintenance(Guid machineId, RecordMaintenanceRequest request, string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = db.Machines.SingleOrDefault(x => x.Id == machineId);
        if (entity is null) return (null, "machine_not_found");
        if (!entity.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_scope_denied");
        entity.LastMaintenanceAt = request.MaintainedAt;
        entity.NextMaintenanceAt = request.NextMaintenanceAt;
        entity.Status = request.Status.Trim();
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    public (MaintenanceWorkOrderDto? WorkOrder, string? Error) CreateMaintenanceWorkOrder(Guid machineId, CreateMaintenanceWorkOrderRequest request, string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var machine = db.Machines.SingleOrDefault(x => x.Id == machineId);
        if (machine is null) return (null, "machine_not_found");
        if (!machine.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_scope_denied");
        if (request.DueAt == default || string.IsNullOrWhiteSpace(request.MaintenanceType)) return (null, "invalid_maintenance_work_order");
        var existing = db.MaintenanceWorkOrders.Any(x => x.MachineId == machineId && x.Status == "Open");
        if (existing) return (null, "maintenance_work_order_open");
        var entity = new ManufacturingMaintenanceWorkOrderEntity
        {
            Id = Guid.NewGuid(), MachineId = machineId, TenantKey = tenantKey, Status = "Open",
            MaintenanceType = request.MaintenanceType.Trim(), DueAt = request.DueAt,
            AssignedTo = string.IsNullOrWhiteSpace(request.AssignedTo) ? null : request.AssignedTo.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(), CreatedAt = DateTimeOffset.UtcNow
        };
        db.MaintenanceWorkOrders.Add(entity);
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = "Manufacturing.MaintenanceWorkOrderCreated.v1",
            Content = JsonSerializer.Serialize(new { eventId = entity.Id, schemaVersion = 1, occurredAt = entity.CreatedAt, correlationId = entity.Id, facilityId = "default", machineId, tenantKey, dueAt = entity.DueAt, maintenanceType = entity.MaintenanceType }),
            OccurredOn = entity.CreatedAt.UtcDateTime, Status = "Pending", RetryCount = 0
        });
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    public (MaintenanceWorkOrderDto? WorkOrder, string? Error) CompleteMaintenanceWorkOrder(Guid machineId, Guid workOrderId, CompleteMaintenanceWorkOrderRequest request, string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var machine = db.Machines.SingleOrDefault(x => x.Id == machineId);
        if (machine is null) return (null, "machine_not_found");
        if (!machine.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_scope_denied");
        var entity = db.MaintenanceWorkOrders.SingleOrDefault(x => x.Id == workOrderId && x.MachineId == machineId && x.TenantKey == tenantKey);
        if (entity is null) return (null, "maintenance_work_order_not_found");
        if (entity.Status != "Open") return (null, "maintenance_work_order_not_open");
        if (string.IsNullOrWhiteSpace(request.Technician) || request.CompletedAt == default || request.CompletedAt < entity.CreatedAt)
            return (null, "invalid_maintenance_completion");
        entity.Status = "Completed";
        entity.Technician = request.Technician.Trim();
        entity.CompletedAt = request.CompletedAt;
        entity.Evidence = string.IsNullOrWhiteSpace(request.Evidence) ? null : request.Evidence.Trim();
        machine.LastMaintenanceAt = request.CompletedAt;
        machine.NextMaintenanceAt = request.NextMaintenanceAt;
        if (machine.Status.Equals("Maintenance", StringComparison.OrdinalIgnoreCase)) machine.Status = "Available";
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = "Manufacturing.MaintenanceWorkOrderCompleted.v1",
            Content = JsonSerializer.Serialize(new { eventId = entity.Id, schemaVersion = 1, occurredAt = request.CompletedAt, correlationId = entity.Id, facilityId = "default", machineId, tenantKey, technician = entity.Technician, nextMaintenanceAt = machine.NextMaintenanceAt }),
            OccurredOn = request.CompletedAt.UtcDateTime, Status = "Pending", RetryCount = 0
        });
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    public IReadOnlyList<MaintenanceWorkOrderDto> GetMaintenanceWorkOrders(string tenantKey, Guid? machineId, string? status, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.MaintenanceWorkOrders.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (machineId.HasValue) query = query.Where(x => x.MachineId == machineId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return query.OrderBy(x => x.DueAt).Take(Math.Clamp(limit, 1, 200)).ToList().Select(ToDto).ToList();
    }

    public IReadOnlyList<MaintenanceWorkOrderDto> GenerateDueMaintenanceWorkOrders(string tenantKey, DateTimeOffset asOf)
    {
        using var db = dbFactory.CreateDbContext();
        using var transaction = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
        var dueMachines = db.Machines
            .Where(x => x.TenantKey == tenantKey && x.Active && x.NextMaintenanceAt != null && x.NextMaintenanceAt <= asOf)
            .ToList();
        var created = new List<ManufacturingMaintenanceWorkOrderEntity>();
        foreach (var machine in dueMachines)
        {
            if (db.MaintenanceWorkOrders.Any(x => x.TenantKey == tenantKey && x.MachineId == machine.Id && x.Status == "Open")) continue;
            var entity = new ManufacturingMaintenanceWorkOrderEntity
            {
                Id = Guid.NewGuid(), MachineId = machine.Id, TenantKey = tenantKey, Status = "Open",
                MaintenanceType = "Preventive", DueAt = machine.NextMaintenanceAt!.Value,
                Notes = "Generated from machine maintenance schedule", CreatedAt = asOf
            };
            db.MaintenanceWorkOrders.Add(entity);
            AddMaintenanceWorkOrderOutbox(db, entity, "Manufacturing.MaintenanceWorkOrderCreated.v1");
            created.Add(entity);
        }
        db.SaveChanges();
        transaction.Commit();
        return created.Select(ToDto).ToList();
    }

    public (DowntimeDto? Downtime, string? Error) CreateDowntime(Guid machineId, CreateDowntimeRequest request, string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var machine = db.Machines.SingleOrDefault(x => x.Id == machineId);
        if (machine is null) return (null, "machine_not_found");
        if (!machine.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_scope_denied");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.StartedAt > DateTimeOffset.UtcNow.AddMinutes(5)) return (null, "invalid_downtime");
        if (db.MachineDowntimes.Any(x => x.MachineId == machineId && x.Status == "Open")) return (null, "machine_downtime_open");
        var entity = new ManufacturingMachineDowntimeEntity
        {
            Id = Guid.NewGuid(), MachineId = machineId, TenantKey = tenantKey, Reason = request.Reason.Trim(), Status = "Open",
            ProductionBatchId = request.ProductionBatchId, OperationExecutionId = request.OperationExecutionId,
            StartedAt = request.StartedAt, Notes = request.Notes?.Trim(), CreatedAt = DateTimeOffset.UtcNow
        };
        machine.Status = "Maintenance";
        db.MachineDowntimes.Add(entity);
        AddDowntimeOutbox(db, entity, "Manufacturing.MachineDowntimeOpened.v1");
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    public (DowntimeDto? Downtime, string? Error) ResolveDowntime(Guid machineId, Guid downtimeId, ResolveDowntimeRequest request, string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var machine = db.Machines.SingleOrDefault(x => x.Id == machineId);
        if (machine is null) return (null, "machine_not_found");
        if (!machine.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_scope_denied");
        var entity = db.MachineDowntimes.SingleOrDefault(x => x.Id == downtimeId && x.MachineId == machineId);
        if (entity is null) return (null, "downtime_not_found");
        if (entity.Status != "Open") return (null, "downtime_not_open");
        if (request.EndedAt < entity.StartedAt || request.EndedAt > DateTimeOffset.UtcNow.AddMinutes(5)) return (null, "invalid_downtime_end");
        entity.Status = "Closed";
        entity.EndedAt = request.EndedAt;
        if (!string.IsNullOrWhiteSpace(request.Notes)) entity.Notes = request.Notes.Trim();
        machine.Status = machine.Active ? "Available" : "Inactive";
        AddDowntimeOutbox(db, entity, "Manufacturing.MachineDowntimeClosed.v1");
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    public IReadOnlyList<DowntimeDto> GetDowntimes(string tenantKey, Guid? machineId, string? status, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.MachineDowntimes.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (machineId.HasValue) query = query.Where(x => x.MachineId == machineId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return query.OrderByDescending(x => x.StartedAt).Take(Math.Clamp(limit, 1, 200)).AsEnumerable().Select(ToDto).ToList();
    }

    private static void AddDowntimeOutbox(ManufacturingDbContext db, ManufacturingMachineDowntimeEntity entity, string type)
    {
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = type,
            Content = JsonSerializer.Serialize(new { eventId = entity.Id, schemaVersion = 1, occurredAt = entity.StartedAt, correlationId = entity.Id, machineId = entity.MachineId, tenantKey = entity.TenantKey, reason = entity.Reason, status = entity.Status, startedAt = entity.StartedAt, endedAt = entity.EndedAt }),
            OccurredOn = DateTime.UtcNow, Status = "Pending"
        });
    }

    private static void AddMaintenanceWorkOrderOutbox(ManufacturingDbContext db, ManufacturingMaintenanceWorkOrderEntity entity, string type)
    {
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = type,
            Content = JsonSerializer.Serialize(new { eventId = entity.Id, schemaVersion = 1, occurredAt = entity.CreatedAt, correlationId = entity.Id, facilityId = "default", machineId = entity.MachineId, tenantKey = entity.TenantKey, dueAt = entity.DueAt, maintenanceType = entity.MaintenanceType }),
            OccurredOn = entity.CreatedAt.UtcDateTime, Status = "Pending", RetryCount = 0
        });
    }

    public IReadOnlyList<EventReceiptDto> GetEventReceipts(string? eventType, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.EventReceipts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(x => x.EventType == eventType);

        return query.OrderByDescending(x => x.ReceivedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(x => new EventReceiptDto(x.Id, x.EventType, x.AggregateId, x.ReceivedAt))
            .ToList();
    }

    public IReadOnlyList<InventoryTransactionDto> GetInventoryTransactions(Guid lotId, string tenantKey, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        return db.InventoryTransactions.AsNoTracking()
            .Where(x => x.LotId == lotId && x.TenantKey == tenantKey)
            .OrderByDescending(x => x.OccurredAt)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(x => new InventoryTransactionDto(x.Id, x.TenantKey, x.LotId, x.TransactionType, x.Quantity, x.Uom, x.FacilityId, x.StockStatus, x.CorrelationId, x.OccurredAt))
            .ToList();
    }

    public ManufacturingDashboardSummaryDto GetDashboardSummary(string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var lots = db.Lots.AsNoTracking().Where(x => x.TenantKey == tenantKey).ToList();
        var transformations = db.Transformations.AsNoTracking().Where(x => x.TenantKey == tenantKey).ToList();
        var orders = db.ProductionOrders.AsNoTracking().Where(x => x.TenantKey == tenantKey).ToList();
        var batches = db.ProductionBatches.AsNoTracking().Where(x => x.TenantKey == tenantKey).ToList();
        var inspections = db.QualityInspections.AsNoTracking().Where(x => x.TenantKey == tenantKey).ToList();
        var released = lots.Where(x => x.Disposition == "Released").Sum(x => x.Quantity);
        var quarantined = lots.Where(x => x.Disposition is "Quarantined" or "Quarantine" or "Hold").Sum(x => x.Quantity);
        return new ManufacturingDashboardSummaryDto(
            tenantKey,
            lots.Count,
            released,
            quarantined,
            transformations.Count,
            transformations.Count == 0 ? 0 : decimal.Round(transformations.Average(x => x.YieldPercent), 2),
            transformations.Sum(x => x.LossQuantity),
            orders.Count(x => x.Status is "Planned" or "Released" or "InProgress"),
            batches.Count(x => x.Status is "Created" or "Started" or "Paused"),
            inspections.Count(x => x.Status == "Pending"),
            inspections.Count(x => x.Status == "Fail"),
            DateTimeOffset.UtcNow);
    }

    public ManufacturingProductionKpiDto GetProductionKpis(string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var completed = db.ProductionBatches.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.Status == "Completed").ToList();
        if (completed.Count == 0)
            return new ManufacturingProductionKpiDto(tenantKey, 0, 0, 0, 0, 0, 0, 0, 0, DateTimeOffset.UtcNow);

        var batchIds = completed.Select(x => x.Id).ToArray();
        var orderIds = completed.Select(x => x.ProductionOrderId).Distinct().ToArray();
        var orders = db.ProductionOrders.AsNoTracking().Where(x => orderIds.Contains(x.Id)).ToDictionary(x => x.Id);
        var recipes = db.Recipes.AsNoTracking().Where(x => orders.Values.Select(x => x.RecipeId).Contains(x.Id)).ToDictionary(x => x.Id);
        var operations = db.OperationExecutions.AsNoTracking().Where(x => batchIds.Contains(x.ProductionBatchId)).ToList();
        var inputs = db.ProductionBatchInputs.AsNoTracking().Where(x => batchIds.Contains(x.ProductionBatchId)).ToList();
        var totalInput = completed.Sum(batch =>
        {
            var reserved = inputs.Where(x => x.ProductionBatchId == batch.Id).Sum(x => x.Quantity);
            return reserved > 0 ? reserved : operations.Where(x => x.ProductionBatchId == batch.Id).Sum(x => x.InputQuantity);
        });
        var actual = completed.Sum(x => x.ActualOutputQuantity);
        var planned = completed.Sum(x => x.PlannedQuantity);
        var target = completed.Average(x => recipes[orders[x.ProductionOrderId].RecipeId].TargetYieldPercent);
        var averageYield = totalInput == 0 ? 0 : decimal.Round(actual / totalInput * 100, 2);
        return new ManufacturingProductionKpiDto(
            tenantKey, completed.Count, planned, actual, totalInput, totalInput - actual,
            averageYield, decimal.Round(target, 2), decimal.Round(averageYield - target, 2), DateTimeOffset.UtcNow);
    }

    public ManufacturingMachineHealthDto GetMachineHealth(string tenantKey, int dueWithinDays)
    {
        using var db = dbFactory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var dueAt = now.AddDays(Math.Clamp(dueWithinDays, 0, 90));
        var machines = db.Machines.AsNoTracking().Where(x => x.TenantKey == tenantKey).ToList();
        return new ManufacturingMachineHealthDto(
            tenantKey,
            machines.Count,
            machines.Count(x => x.Active && x.Status.Equals("Available", StringComparison.OrdinalIgnoreCase)),
            machines.Count(x => x.Active && x.Status.Equals("Running", StringComparison.OrdinalIgnoreCase)),
            machines.Count(x => x.Active && x.Status.Equals("Maintenance", StringComparison.OrdinalIgnoreCase)),
            machines.Count(x => !x.Active),
            machines.Count(x => x.Active && x.NextMaintenanceAt is { } next && next <= now),
            machines.Count(x => x.Active && x.NextMaintenanceAt is { } next && next > now && next <= dueAt),
            now);
    }

    public ManufacturingOeeDto GetOee(string tenantKey, Guid? machineId)
    {
        using var db = dbFactory.CreateDbContext();
        var completed = db.ProductionBatches.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.Status == "Completed" && (!machineId.HasValue || x.MachineId == machineId))
            .ToList();
        var batchIds = completed.Select(x => x.Id).ToArray();
        var operations = db.OperationExecutions.AsNoTracking().Where(x => batchIds.Contains(x.ProductionBatchId)).ToList();
        var plannedMinutes = completed.Sum(x => x.StartedAt.HasValue && x.CompletedAt.HasValue
            ? Math.Max(0, (decimal)(x.CompletedAt.Value - x.StartedAt.Value).TotalMinutes) : 0m);
        var runMinutes = operations.Sum(x => x.StartedAt.HasValue && x.CompletedAt.HasValue
            ? Math.Max(0, (decimal)(x.CompletedAt.Value - x.StartedAt.Value).TotalMinutes) : 0m);
        var goodQuantity = completed.Sum(x => x.ActualOutputQuantity);
        var rejectQuantity = operations.Sum(x => x.LossQuantity);
        var missing = new List<string>();
        if (plannedMinutes <= 0) missing.Add("planned_production_time");
        if (runMinutes <= 0) missing.Add("run_time");
        if (goodQuantity + rejectQuantity <= 0) missing.Add("good_reject_count");
        missing.Add("ideal_rate");
        var availability = plannedMinutes > 0 ? (decimal?)decimal.Round(runMinutes / plannedMinutes * 100, 2) : null;
        var quality = goodQuantity + rejectQuantity > 0 ? (decimal?)decimal.Round(goodQuantity / (goodQuantity + rejectQuantity) * 100, 2) : null;
        return new ManufacturingOeeDto(
            tenantKey, machineId, missing.Count == 0 ? "complete" : "insufficient-data",
            null, availability, null, quality, decimal.Round(plannedMinutes, 2), decimal.Round(runMinutes, 2),
            decimal.Round(goodQuantity, 3), decimal.Round(rejectQuantity, 3), null, missing, DateTimeOffset.UtcNow);
    }

    public ManufacturingProductionCostDto GetProductionCosts(string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var completed = db.ProductionBatches.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.Status == "Completed").ToList();
        if (completed.Count == 0)
            return new ManufacturingProductionCostDto(tenantKey, 0, 0, 0, [], DateTimeOffset.UtcNow);

        var outputQuantity = completed.Sum(x => x.ActualOutputQuantity);
        var outputLotIds = completed.Where(x => x.OutputLotId.HasValue).Select(x => x.OutputLotId!.Value).ToArray();
        var transformationIds = db.Transformations.AsNoTracking()
            .Where(x => outputLotIds.Contains(x.OutputLotId))
            .Select(x => x.Id)
            .ToArray();
        var issueTransactions = db.InventoryTransactions.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.TransactionType == "Issue" && transformationIds.Contains(x.CorrelationId))
            .Select(x => new { x.LotId, x.Quantity })
            .ToList();
        var lotIds = issueTransactions.Select(x => x.LotId).Distinct().ToArray();
        var lots = db.Lots.AsNoTracking().Where(x => lotIds.Contains(x.Id)).ToDictionary(x => x.Id);
        var skus = lots.Values.Select(x => x.Sku).Distinct().ToArray();
        var prices = db.PurchaseOrderLines.AsNoTracking()
            .Join(db.PurchaseOrders.AsNoTracking().Where(x => x.TenantKey == tenantKey),
                line => line.PurchaseOrderId, order => order.Id, (line, _) => line)
            .Where(x => skus.Contains(x.MaterialSku))
            .GroupBy(x => x.MaterialSku)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(x => x.OrderedQuantity) == 0
                    ? 0
                    : group.Sum(x => x.OrderedQuantity * x.UnitPrice) / group.Sum(x => x.OrderedQuantity));
        var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cost = 0m;
        foreach (var issue in issueTransactions)
        {
            var sku = lots[issue.LotId].Sku;
            if (!prices.TryGetValue(sku, out var unitPrice))
            {
                missing.Add(sku);
                continue;
            }
            cost += issue.Quantity * unitPrice;
        }
        return new ManufacturingProductionCostDto(
            tenantKey, completed.Count, decimal.Round(cost, 2),
            outputQuantity == 0 ? 0 : decimal.Round(cost / outputQuantity, 2),
            missing.OrderBy(x => x).ToList(), DateTimeOffset.UtcNow);
    }

    public IReadOnlyList<ManufacturingExecutiveExceptionDto> GetExecutiveExceptions(string tenantKey, int expiryWithinDays, int downtimeThresholdHours)
    {
        using var db = dbFactory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var expiryAt = DateOnly.FromDateTime(now.UtcDateTime).AddDays(Math.Clamp(expiryWithinDays, 0, 365));
        var exceptions = new List<ManufacturingExecutiveExceptionDto>();
        foreach (var lot in db.Lots.AsNoTracking().Where(x => x.TenantKey == tenantKey).ToList())
        {
            if (!lot.Disposition.Equals("Released", StringComparison.OrdinalIgnoreCase))
                exceptions.Add(new("lot_hold", "High", "Lot is not released", $"SKU {lot.Sku} has disposition {lot.Disposition} and is excluded from ATP.", lot.Id, lot.CreatedAt));
            else if (lot.BestBefore is { } bestBefore && bestBefore <= expiryAt)
                exceptions.Add(new("expiry_risk", "Medium", "Lot expiry risk", $"SKU {lot.Sku} expires on {bestBefore:yyyy-MM-dd}.", lot.Id, lot.CreatedAt));
        }
        foreach (var inspection in db.QualityInspections.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.Status == "Pending").ToList())
            exceptions.Add(new("pending_quality", "High", "Quality inspection pending", $"Lot {inspection.LotId} is waiting for quality disposition.", inspection.LotId, inspection.InspectedAt));
        foreach (var downtime in db.MachineDowntimes.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.Status == "Open" && x.StartedAt <= now.AddHours(-Math.Clamp(downtimeThresholdHours, 1, 720))).ToList())
            exceptions.Add(new("prolonged_downtime", "High", "Machine downtime prolonged", $"Machine {downtime.MachineId} has been down since {downtime.StartedAt:O}.", downtime.Id, downtime.StartedAt));
        var latestTelemetry = db.MachineTelemetry.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey)
            .ToList()
            .GroupBy(x => x.MachineId)
            .Select(group => group.OrderByDescending(x => x.ObservedAt).ThenByDescending(x => x.ReceivedAt).First());
        foreach (var telemetry in latestTelemetry.Where(x => x.State is "Fault" or "UnplannedDown"))
            exceptions.Add(new("machine_telemetry_fault", "High", "Machine telemetry fault", $"Machine {telemetry.MachineId} reported state {telemetry.State} from {telemetry.Source} at {telemetry.ObservedAt:O}.", telemetry.MachineId, telemetry.ObservedAt));
        foreach (var recipe in db.Recipes.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.Status == "Submitted").ToList())
            exceptions.Add(new("recipe_approval", "Medium", "Recipe approval pending", $"Recipe {recipe.ProductSku} v{recipe.Version} is submitted for approval.", recipe.Id, recipe.CreatedAt));
        var reviewedLossOperationIds = db.LossReviews.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.Decision == "Approved")
            .Select(x => x.OperationExecutionId)
            .ToHashSet();
        foreach (var loss in db.OutboxMessages.AsNoTracking().Where(x => x.Type == "Manufacturing.LossThresholdExceeded.v1" && x.Status == "Pending" && x.Content.Contains($"\"tenantKey\":\"{tenantKey}\"")).ToList())
        {
            using var document = JsonDocument.Parse(loss.Content);
            var operationId = document.RootElement.TryGetProperty("operationId", out var operationProperty) && operationProperty.TryGetGuid(out var parsedOperationId)
                ? parsedOperationId
                : Guid.Empty;
            if (reviewedLossOperationIds.Contains(operationId)) continue;
            exceptions.Add(new("loss_threshold", "High", "Yield below recipe target", "A production operation requires supervisor review before cost/QC close.", loss.Id, new DateTimeOffset(loss.OccurredOn, TimeSpan.Zero)));
        }
        return exceptions.OrderBy(x => x.Severity == "High" ? 0 : x.Severity == "Medium" ? 1 : 2).ThenBy(x => x.OccurredAt).Take(100).ToList();
    }

    public (LossReviewDto? Review, string? Error) ReviewLoss(string tenantKey, Guid batchId, Guid operationId, LossReviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reviewer) || request.Decision is not ("Approved" or "Rejected"))
            return (null, "invalid_loss_review");
        using var db = dbFactory.CreateDbContext();
        var batch = db.ProductionBatches.SingleOrDefault(x => x.Id == batchId && x.TenantKey == tenantKey);
        if (batch is null) return (null, "production_batch_not_found");
        var operation = db.OperationExecutions.SingleOrDefault(x => x.Id == operationId && x.ProductionBatchId == batchId);
        if (operation is null) return (null, "operation_not_found");
        var review = db.LossReviews.SingleOrDefault(x => x.OperationExecutionId == operationId);
        var now = DateTimeOffset.UtcNow;
        if (review is null)
        {
            review = new ManufacturingLossReviewEntity
            {
                Id = Guid.NewGuid(), TenantKey = tenantKey, ProductionBatchId = batchId, OperationExecutionId = operationId,
                Decision = request.Decision, Reviewer = request.Reviewer.Trim(), Notes = request.Notes?.Trim(), ReviewedAt = now
            };
            db.LossReviews.Add(review);
        }
        else
        {
            review.Decision = request.Decision;
            review.Reviewer = request.Reviewer.Trim();
            review.Notes = request.Notes?.Trim();
            review.ReviewedAt = now;
        }
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = "Manufacturing.LossThresholdReviewed.v1",
            Content = JsonSerializer.Serialize(new { eventId = review.Id, schemaVersion = 1, occurredAt = now, correlationId = batchId, productionBatchId = batchId, operationId, tenantKey, decision = review.Decision, reviewer = review.Reviewer }),
            OccurredOn = now.UtcDateTime, Status = "Pending"
        });
        db.SaveChanges();
        return (new LossReviewDto(review.Id, review.TenantKey, review.ProductionBatchId, review.OperationExecutionId, review.Decision, review.Reviewer, review.Notes, review.ReviewedAt), null);
    }

    public IReadOnlyList<ManufacturingMaterialRequirementDto> GetMaterialRequirements(string tenantKey, Guid? productionOrderId)
    {
        using var db = dbFactory.CreateDbContext();
        var orders = db.ProductionOrders.AsNoTracking().Where(x => x.TenantKey == tenantKey && (x.Status == "Planned" || x.Status == "Open" || x.Status == "Released"));
        if (productionOrderId.HasValue) orders = orders.Where(x => x.Id == productionOrderId.Value);
        var orderRows = orders.ToList();
        var recipeIds = orderRows.Select(x => x.RecipeId).Distinct().ToArray();
        var recipes = db.Recipes.AsNoTracking().Include(x => x.Components).Where(x => recipeIds.Contains(x.Id) && x.Status == "Approved").ToDictionary(x => x.Id);
        var skus = recipes.Values.SelectMany(x => x.Components.Select(c => c.IngredientSku)).Distinct().ToArray();
        var lots = db.Lots.AsNoTracking().Where(x => x.TenantKey == tenantKey && skus.Contains(x.Sku) && x.Disposition == "Released").ToList();
        var lotIds = lots.Select(x => x.Id).ToArray();
        var now = DateTimeOffset.UtcNow;
        var reservations = db.LotReservations.AsNoTracking().Where(x => lotIds.Contains(x.LotId) && x.Status == "Reserved" && (x.ExpiresAt == null || x.ExpiresAt > now)).ToList();
        var result = new List<ManufacturingMaterialRequirementDto>();
        foreach (var order in orderRows)
        {
            if (!recipes.TryGetValue(order.RecipeId, out var recipe)) continue;
            foreach (var component in recipe.Components)
            {
                var matchingLots = lots.Where(x => x.Sku.Equals(component.IngredientSku, StringComparison.OrdinalIgnoreCase)).ToList();
                var released = matchingLots.Sum(x => x.Quantity);
                var reserved = reservations.Where(x => matchingLots.Any(l => l.Id == x.LotId)).Sum(x => x.Quantity);
                var required = decimal.Round(order.TargetQuantity * component.Quantity, 3);
                var available = Math.Max(0, released - reserved);
                result.Add(new(tenantKey, order.Id.ToString(), order.OrderNumber, component.IngredientSku, required, released, reserved, available, Math.Max(0, required - available), component.Uom, now));
            }
        }
        return result;
    }

    private static LotDto ToDto(ManufacturingLotEntity x) => new(x.Id, x.TenantKey, x.Sku, x.Quantity, x.Uom, x.Disposition, x.BestBefore, x.CreatedAt);
    private static TransformationDto ToDto(ManufacturingTransformationEntity x, IReadOnlyList<TransformationInput> inputs, LotDto output) =>
        new(x.Id, x.TenantKey, x.ProcessStep, x.RecipeId, x.MachineId, inputs, output, x.InputQuantity, x.YieldPercent, x.LossQuantity, x.CreatedAt);
    private static RecipeDto ToDto(ManufacturingRecipeEntity x) =>
        new(x.Id, x.TenantKey, x.ProductSku, x.Version, x.ProcessStep, x.OutputUom, x.TargetYieldPercent, x.Active, x.Status,
            x.EffectiveFrom, x.EffectiveTo, x.ApprovedBy, x.ApprovedAt,
            x.Components.Select(c => new RecipeComponentDto(c.IngredientSku, c.Quantity, c.Uom)).ToList(), x.CreatedAt, x.ProductSpecificationId);
    private static MachineDto ToDto(ManufacturingMachineEntity x) =>
        new(x.Id, x.TenantKey, x.Code, x.Name, x.Status, x.LastMaintenanceAt, x.NextMaintenanceAt, x.Active, x.CreatedAt);
    private static MachineTelemetryDto ToDto(ManufacturingMachineTelemetryEntity x) =>
        new(x.Id, x.EventId, x.MachineId, x.TenantKey, x.Source, x.State, x.MeterName, x.MeterValue, x.Sequence, x.ObservedAt, x.ReceivedAt);
    private static DowntimeDto ToDto(ManufacturingMachineDowntimeEntity x) =>
        new(x.Id, x.MachineId, x.TenantKey, x.Reason, x.Status, x.ProductionBatchId, x.OperationExecutionId, x.StartedAt, x.EndedAt, x.Notes, x.CreatedAt);
    private static MaintenanceWorkOrderDto ToDto(ManufacturingMaintenanceWorkOrderEntity x) =>
        new(x.Id, x.MachineId, x.TenantKey, x.Status, x.MaintenanceType, x.DueAt, x.AssignedTo, x.Notes, x.Technician, x.CompletedAt, x.Evidence, x.CreatedAt);
    private static QualityInspectionDto ToDto(ManufacturingQualityInspectionEntity x) =>
        new(x.Id, x.LotId, x.TenantKey, x.Status, x.MoisturePercent, x.Inspector, x.Notes, x.InspectedAt);
}

public sealed class ManufacturingOutboxMessageEntity
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime OccurredOn { get; set; }
    public DateTime? ProcessedOn { get; set; }
    public string Status { get; set; } = "Pending";
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
    public string Status { get; set; } = "Approved";
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
    public string Decision { get; set; } = "Approved";
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

public sealed class ManufacturingQualityInspectionEntity
{
    public Guid Id { get; set; }
    public Guid LotId { get; set; }
    public string TenantKey { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public decimal MoisturePercent { get; set; }
    public string Inspector { get; set; } = "";
    public string? Notes { get; set; }
    public DateTimeOffset InspectedAt { get; set; }
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
    public string Status { get; set; } = "Draft";
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
