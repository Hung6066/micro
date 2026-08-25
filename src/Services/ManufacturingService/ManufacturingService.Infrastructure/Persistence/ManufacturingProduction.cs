using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using His.Hope.ManufacturingService.Application;

public sealed class ManufacturingProductionStore(IDbContextFactory<ManufacturingDbContext> dbFactory)
{
    public (ProductionOrderDto? Order, string? Error) CreateOrder(string tenantKey, CreateProductionOrderRequest request)
    {
        var orderPolicyError = ProductionPolicy.ValidateOrder(new ProductionOrderValidationInput(request.OrderNumber, request.ProductSku, request.RecipeId, request.TargetQuantity, request.OutputUom));
        if (orderPolicyError is not null) return (null, orderPolicyError);
        using var db = dbFactory.CreateDbContext();
        if (db.ProductionOrders.Any(x => x.TenantKey == tenantKey && x.OrderNumber == request.OrderNumber.Trim())) return (null, "production_order_exists");
        var recipe = db.Recipes.SingleOrDefault(x => x.Id == request.RecipeId);
        if (recipe is null) return (null, "recipe_not_found");
        if (!recipe.Active || !recipe.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "recipe_unavailable");
        if (!recipe.ProductSku.Equals(request.ProductSku, StringComparison.OrdinalIgnoreCase)) return (null, "recipe_product_mismatch");
        var entity = new ManufacturingProductionOrderEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, OrderNumber = request.OrderNumber.Trim(), ProductSku = recipe.ProductSku,
            RecipeId = recipe.Id, RecipeVersion = recipe.Version, TargetQuantity = request.TargetQuantity, OutputUom = request.OutputUom.Trim(),
            Status = "Planned", CreatedAt = DateTimeOffset.UtcNow
        };
        db.ProductionOrders.Add(entity);
        db.SaveChanges();
        return (ToDto(entity, recipe), null);
    }

    public (ProductionOrderDto? Order, string? Error) ReleaseOrder(string tenantKey, Guid orderId)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = db.ProductionOrders.SingleOrDefault(x => x.Id == orderId);
        if (entity is null) return (null, "production_order_not_found");
        if (!entity.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_mismatch");
        if (entity.Status != "Planned") return (ToDto(entity, db.Recipes.Single(x => x.Id == entity.RecipeId)), null);
        entity.Status = "Released";
        entity.ReleasedAt = DateTimeOffset.UtcNow;
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = "Manufacturing.ProductionOrderReleased.v1",
            Content = JsonSerializer.Serialize(new { eventId = entity.Id, schemaVersion = 1, occurredAt = entity.ReleasedAt, correlationId = entity.Id, facilityId = "default", productionOrderId = entity.Id, tenantKey, productSku = entity.ProductSku, recipeId = entity.RecipeId, recipeVersion = entity.RecipeVersion, targetQuantity = entity.TargetQuantity }),
            OccurredOn = entity.ReleasedAt.Value.UtcDateTime, Status = "Pending"
        });
        db.SaveChanges();
        return (ToDto(entity, db.Recipes.Single(x => x.Id == entity.RecipeId)), null);
    }

    public IReadOnlyList<ProductionOrderDto> GetOrders(string tenantKey, string? status, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.ProductionOrders.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return query.OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(limit, 1, 200)).AsEnumerable()
            .Select(x => ToDto(x, db.Recipes.AsNoTracking().Single(r => r.Id == x.RecipeId))).ToList();
    }

    public (ProductionBatchDto? Batch, string? Error) CreateBatch(string tenantKey, CreateProductionBatchRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        var order = db.ProductionOrders.SingleOrDefault(x => x.Id == request.ProductionOrderId);
        if (order is null) return (null, "production_order_not_found");
        if (!order.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_mismatch");
        var plannedQuantity = request.PlannedQuantity ?? order.TargetQuantity;
        var batchPolicyError = ProductionPolicy.ValidateBatch(new ProductionBatchValidationInput(order.Status, request.BatchNumber, plannedQuantity));
        if (batchPolicyError is not null) return (null, batchPolicyError);
        if (db.ProductionBatches.Any(x => x.TenantKey == tenantKey && x.BatchNumber == request.BatchNumber.Trim())) return (null, "production_batch_exists");
        var entity = new ManufacturingProductionBatchEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, ProductionOrderId = order.Id, BatchNumber = request.BatchNumber.Trim(),
            Status = "Created", PlannedQuantity = plannedQuantity, ActualOutputQuantity = 0,
            MachineId = request.MachineId, CreatedAt = DateTimeOffset.UtcNow
        };
        if (entity.MachineId.HasValue)
        {
            var machine = db.Machines.SingleOrDefault(x => x.Id == entity.MachineId.Value);
            if (machine is null) return (null, "machine_not_found");
            if (!machine.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase) || !machine.Active) return (null, "machine_unavailable");
        }
        db.ProductionBatches.Add(entity);
        db.SaveChanges();
        return (ToDto(entity, []), null);
    }

    public IReadOnlyList<ProductionBatchDto> GetBatches(string tenantKey, string? status, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.ProductionBatches.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var batches = query.OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(limit, 1, 200)).ToList();
        var ids = batches.Select(x => x.Id).ToArray();
        var operations = db.OperationExecutions.AsNoTracking().Where(x => ids.Contains(x.ProductionBatchId)).ToList();
        return batches.Select(x => ToDto(x, operations.Where(o => o.ProductionBatchId == x.Id))).ToList();
    }

    public (ProductionBatchDto? Batch, string? Error) ChangeBatchStatus(string tenantKey, Guid batchId, string targetStatus)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = db.ProductionBatches.SingleOrDefault(x => x.Id == batchId);
        if (entity is null) return (null, "production_batch_not_found");
        if (!entity.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_mismatch");
        var order = db.ProductionOrders.Single(x => x.Id == entity.ProductionOrderId);
        var machineAvailable = entity.MachineId is null;
        if (targetStatus == "Started" && entity.MachineId is { } machineId)
        {
            var machine = db.Machines.SingleOrDefault(x => x.Id == machineId);
            machineAvailable = machine is not null && machine.Active && machine.Status.Equals("Available", StringComparison.OrdinalIgnoreCase);
        }
        var operations = db.OperationExecutions.Where(x => x.ProductionBatchId == batchId).ToList();
        var transitionError = ProductionPolicy.ValidateTransition(new BatchTransitionValidationInput(
            entity.Status,
            targetStatus,
            operations.Count > 0,
            operations.Where(x => x.Required).All(x => x.Status == "Completed"),
            operations.Where(x => x.Required).All(x => x.Status == "Completed" && x.QcStatus == "Pass"),
            machineAvailable));
        if (transitionError is not null) return (null, transitionError);
        entity.Status = targetStatus;
        if (targetStatus == "Started") order.Status = "InProgress";
        if (targetStatus == "Completed") order.Status = "Completed";
        if (targetStatus == "Started" && entity.StartedAt is null) entity.StartedAt = DateTimeOffset.UtcNow;
        if (targetStatus == "Completed") entity.CompletedAt = DateTimeOffset.UtcNow;
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = $"Manufacturing.ProductionBatch{targetStatus}.v1",
            Content = JsonSerializer.Serialize(new { eventId = entity.Id, schemaVersion = 1, occurredAt = DateTimeOffset.UtcNow, correlationId = entity.Id, facilityId = "default", productionBatchId = entity.Id, tenantKey, status = targetStatus }),
            OccurredOn = DateTime.UtcNow, Status = "Pending"
        });
        db.SaveChanges();
        return (ToDto(entity, db.OperationExecutions.AsNoTracking().Where(x => x.ProductionBatchId == entity.Id).ToList()), null);
    }

    public (OperationExecutionDto? Operation, string? Error) RecordOperation(string tenantKey, Guid batchId, RecordOperationRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        var batch = db.ProductionBatches.SingleOrDefault(x => x.Id == batchId);
        if (batch is null) return (null, "production_batch_not_found");
        if (!batch.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "tenant_mismatch");
        var operationPolicyError = ProductionPolicy.ValidateOperation(
            new ProductionBatchValidationInput(batch.Status, "batch", batch.PlannedQuantity),
            new OperationMeasurementValidationInput(request.Sequence, request.ProcessStep, request.Operator, request.InputQuantity, request.OutputQuantity, request.QcStatus));
        if (operationPolicyError is not null) return (null, operationPolicyError);
        if (db.OperationExecutions.Any(x => x.ProductionBatchId == batchId && x.Sequence == request.Sequence)) return (null, "operation_sequence_exists");
        var entity = new ManufacturingOperationExecutionEntity
        {
            Id = Guid.NewGuid(), ProductionBatchId = batchId, Sequence = request.Sequence, ProcessStep = request.ProcessStep.Trim(),
            Operator = request.Operator.Trim(), InputQuantity = request.InputQuantity, OutputQuantity = request.OutputQuantity,
            LossQuantity = request.InputQuantity - request.OutputQuantity, Status = "Completed", Required = request.Required,
            QcStatus = request.QcStatus.Trim(), StartedAt = request.StartedAt ?? DateTimeOffset.UtcNow, CompletedAt = DateTimeOffset.UtcNow
        };
        batch.ActualOutputQuantity += entity.OutputQuantity;
        db.OperationExecutions.Add(entity);
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = "Manufacturing.OperationMeasurementRecorded.v1",
            Content = JsonSerializer.Serialize(new { eventId = entity.Id, schemaVersion = 1, occurredAt = entity.CompletedAt, correlationId = entity.Id, facilityId = "default", productionBatchId = batchId, operationId = entity.Id, processStep = entity.ProcessStep, inputQuantity = entity.InputQuantity, outputQuantity = entity.OutputQuantity, lossQuantity = entity.LossQuantity, qcStatus = entity.QcStatus, tenantKey }),
            OccurredOn = entity.CompletedAt.Value.UtcDateTime, Status = "Pending"
        });
        db.SaveChanges();
        return (ToDto(entity), null);
    }

    private static ProductionOrderDto ToDto(ManufacturingProductionOrderEntity x, ManufacturingRecipeEntity recipe) => new(x.Id, x.TenantKey, x.OrderNumber, x.ProductSku, x.RecipeId, x.RecipeVersion, x.TargetQuantity, x.OutputUom, x.Status, x.CreatedAt, x.ReleasedAt);
    private static ProductionBatchDto ToDto(ManufacturingProductionBatchEntity x, IEnumerable<ManufacturingOperationExecutionEntity> operations) => new(x.Id, x.TenantKey, x.ProductionOrderId, x.BatchNumber, x.Status, x.PlannedQuantity, x.ActualOutputQuantity, x.MachineId, x.CreatedAt, x.StartedAt, x.CompletedAt, operations.Select(ToDto).ToList());
    private static OperationExecutionDto ToDto(ManufacturingOperationExecutionEntity x) => new(x.Id, x.ProductionBatchId, x.Sequence, x.ProcessStep, x.Operator, x.InputQuantity, x.OutputQuantity, x.LossQuantity, x.Status, x.Required, x.QcStatus, x.StartedAt, x.CompletedAt);
}

public sealed class ManufacturingProductionOrderEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public string OrderNumber { get; set; } = "";
    public string ProductSku { get; set; } = "";
    public Guid RecipeId { get; set; }
    public int RecipeVersion { get; set; }
    public decimal TargetQuantity { get; set; }
    public string OutputUom { get; set; } = "";
    public string Status { get; set; } = "Planned";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
}

public sealed class ManufacturingProductionBatchEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = "";
    public Guid ProductionOrderId { get; set; }
    public string BatchNumber { get; set; } = "";
    public string Status { get; set; } = "Created";
    public decimal PlannedQuantity { get; set; }
    public decimal ActualOutputQuantity { get; set; }
    public Guid? MachineId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class ManufacturingOperationExecutionEntity
{
    public Guid Id { get; set; }
    public Guid ProductionBatchId { get; set; }
    public int Sequence { get; set; }
    public string ProcessStep { get; set; } = "";
    public string Operator { get; set; } = "";
    public decimal InputQuantity { get; set; }
    public decimal OutputQuantity { get; set; }
    public decimal LossQuantity { get; set; }
    public string Status { get; set; } = "Completed";
    public bool Required { get; set; } = true;
    public string QcStatus { get; set; } = "Pending";
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
