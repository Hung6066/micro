using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
    public DbSet<ManufacturingQualityInspectionEntity> QualityInspections => Set<ManufacturingQualityInspectionEntity>();
    public DbSet<ManufacturingSupplierEntity> Suppliers => Set<ManufacturingSupplierEntity>();
    public DbSet<ManufacturingPurchaseOrderEntity> PurchaseOrders => Set<ManufacturingPurchaseOrderEntity>();
    public DbSet<ManufacturingPurchaseOrderLineEntity> PurchaseOrderLines => Set<ManufacturingPurchaseOrderLineEntity>();
    public DbSet<ManufacturingInboundReceiptEntity> InboundReceipts => Set<ManufacturingInboundReceiptEntity>();
    public DbSet<ManufacturingInventoryTransactionEntity> InventoryTransactions => Set<ManufacturingInventoryTransactionEntity>();
    public DbSet<ManufacturingLotReservationEntity> LotReservations => Set<ManufacturingLotReservationEntity>();
    public DbSet<ManufacturingProductionOrderEntity> ProductionOrders => Set<ManufacturingProductionOrderEntity>();
    public DbSet<ManufacturingProductionBatchEntity> ProductionBatches => Set<ManufacturingProductionBatchEntity>();
    public DbSet<ManufacturingOperationExecutionEntity> OperationExecutions => Set<ManufacturingOperationExecutionEntity>();

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

public sealed partial class PostgresManufacturingStore(IDbContextFactory<ManufacturingDbContext> dbFactory)
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
            if (!recipe.Active) return (null, "recipe_inactive");
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
        var quantity = db.Lots.Where(x => x.TenantKey == tenantKey && x.Sku == sku && x.Disposition == "Released").Sum(x => (decimal?)x.Quantity) ?? 0;
        var uom = db.Lots.Where(x => x.TenantKey == tenantKey && x.Sku == sku).Select(x => x.Uom).FirstOrDefault() ?? "kg";
        return new AvailabilityDto(tenantKey, sku, quantity, uom, DateTimeOffset.UtcNow);
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
        var lot = db.Lots.SingleOrDefault(x => x.Id == lotId);
        if (lot is null) return (null, "lot_not_found");
        if (!lot.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_scope_denied");
        lot.Disposition = normalized;
        var dispositionEventId = Guid.NewGuid();
        db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
        {
            Id = Guid.NewGuid(), TenantKey = lot.TenantKey, LotId = lot.Id,
            TransactionType = normalized.Equals("Released", StringComparison.OrdinalIgnoreCase) ? "Release" : "Hold",
            Quantity = lot.Quantity, Uom = lot.Uom, FacilityId = "default", StockStatus = lot.Disposition,
            CorrelationId = dispositionEventId, OccurredAt = DateTimeOffset.UtcNow
        });
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = dispositionEventId, Type = "Manufacturing.LotDispositionChanged.v1",
            Content = JsonSerializer.Serialize(new { eventId = dispositionEventId, schemaVersion = 1, occurredAt = DateTimeOffset.UtcNow, correlationId = dispositionEventId, facilityId = (string?)null, lotId = lot.Id, tenantKey = lot.TenantKey, disposition = lot.Disposition }),
            OccurredOn = DateTime.UtcNow, Status = "Pending"
        });
        db.SaveChanges();
        return (ToDto(lot), null);
    }

    private static readonly string[] AllowedLotDispositions = ["Released", "Quarantined", "Rejected", "Consumed"];

    public (QualityInspectionDto? Inspection, string? Error) CreateQualityInspection(CreateQualityInspectionRequest request)
    {
        var normalizedStatus = request.Status.Trim();
        if (!AllowedInspectionStatuses.Contains(normalizedStatus, StringComparer.OrdinalIgnoreCase)) return (null, "invalid_inspection_status");
        if (request.MoisturePercent is < 0 or > 100) return (null, "invalid_moisture_percent");
        using var db = dbFactory.CreateDbContext();
        var lot = db.Lots.SingleOrDefault(x => x.Id == request.LotId);
        if (lot is null) return (null, "lot_not_found");
        if (!lot.TenantKey.Equals(request.TenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_mismatch");
        var entity = new ManufacturingQualityInspectionEntity
        {
            Id = Guid.NewGuid(), LotId = lot.Id, TenantKey = lot.TenantKey, Status = normalizedStatus,
            MoisturePercent = request.MoisturePercent, Inspector = request.Inspector.Trim(), Notes = request.Notes?.Trim(),
            InspectedAt = request.InspectedAt ?? DateTimeOffset.UtcNow
        };
        db.QualityInspections.Add(entity);
        var dispositionChanged = false;
        var dispositionEventId = Guid.NewGuid();
        if (normalizedStatus.Equals("Pass", StringComparison.OrdinalIgnoreCase) &&
            (lot.Disposition.Equals("Quarantine", StringComparison.OrdinalIgnoreCase) || lot.Disposition.Equals("Hold", StringComparison.OrdinalIgnoreCase)))
        {
            lot.Disposition = "Released";
            dispositionChanged = true;
        }
        else if (normalizedStatus.Equals("Fail", StringComparison.OrdinalIgnoreCase) &&
                 lot.Disposition.Equals("Released", StringComparison.OrdinalIgnoreCase))
        {
            lot.Disposition = "Quarantine";
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
        var entity = new ManufacturingRecipeEntity
        {
            Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), ProductSku = request.ProductSku.Trim(),
            Version = request.Version, ProcessStep = request.ProcessStep.Trim(), OutputUom = request.OutputUom.Trim(),
            TargetYieldPercent = request.TargetYieldPercent, Active = request.Active, CreatedAt = DateTimeOffset.UtcNow,
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
        var quarantined = lots.Where(x => x.Disposition is "Quarantine" or "Hold").Sum(x => x.Quantity);
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

    private static LotDto ToDto(ManufacturingLotEntity x) => new(x.Id, x.TenantKey, x.Sku, x.Quantity, x.Uom, x.Disposition, x.BestBefore, x.CreatedAt);
    private static TransformationDto ToDto(ManufacturingTransformationEntity x, IReadOnlyList<TransformationInput> inputs, LotDto output) =>
        new(x.Id, x.TenantKey, x.ProcessStep, x.RecipeId, x.MachineId, inputs, output, x.InputQuantity, x.YieldPercent, x.LossQuantity, x.CreatedAt);
    private static RecipeDto ToDto(ManufacturingRecipeEntity x) =>
        new(x.Id, x.TenantKey, x.ProductSku, x.Version, x.ProcessStep, x.OutputUom, x.TargetYieldPercent, x.Active,
            x.Components.Select(c => new RecipeComponentDto(c.IngredientSku, c.Quantity, c.Uom)).ToList(), x.CreatedAt);
    private static MachineDto ToDto(ManufacturingMachineEntity x) =>
        new(x.Id, x.TenantKey, x.Code, x.Name, x.Status, x.LastMaintenanceAt, x.NextMaintenanceAt, x.Active, x.CreatedAt);
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
