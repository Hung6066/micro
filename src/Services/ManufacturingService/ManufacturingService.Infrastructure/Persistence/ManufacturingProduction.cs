using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.ManufacturingService.Application;

public sealed class ManufacturingProductionStore(IDbContextFactory<ManufacturingDbContext> dbFactory) : IManufacturingProductionOrderStore
{
    public async Task<(ProductionOrderDto? Order, string? Error)> CreateOrderAsync(string tenantKey, CreateProductionOrderRequest request, CancellationToken cancellationToken = default)
    {
        var orderPolicyError = ProductionPolicy.ValidateOrder(new ProductionOrderValidationInput(request.OrderNumber, request.ProductSku, request.RecipeId, request.TargetQuantity, request.OutputUom));
        if (orderPolicyError is not null) return (null, orderPolicyError);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.ProductionOrders.AnyAsync(x => x.TenantKey == tenantKey && x.OrderNumber == request.OrderNumber.Trim(), cancellationToken)) return (null, ManufacturingErrorCodes.ProductionOrderExists);
        var recipe = await db.Recipes.SingleOrDefaultAsync(x => x.Id == request.RecipeId, cancellationToken);
        if (recipe is null) return (null, ManufacturingErrorCodes.RecipeNotFound);
        if (!recipe.Active || !recipe.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.RecipeUnavailable);
        if (!recipe.ProductSku.Equals(request.ProductSku, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.RecipeProductMismatch);
        var entity = new ManufacturingProductionOrderEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, OrderNumber = request.OrderNumber.Trim(), ProductSku = recipe.ProductSku,
            RecipeId = recipe.Id, RecipeVersion = recipe.Version, TargetQuantity = request.TargetQuantity, OutputUom = request.OutputUom.Trim(),
            Status = "Planned", CreatedAt = DateTimeOffset.UtcNow
        };
        db.ProductionOrders.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(entity, recipe), null);
    }

    public async Task<(ProductionOrderDto? Order, string? Error)> ReleaseOrderAsync(string tenantKey, Guid orderId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.ProductionOrders.SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (entity is null) return (null, ManufacturingErrorCodes.ProductionOrderNotFound);
        if (!entity.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantMismatch);
        if (entity.Status != "Planned") return (ToDto(entity, await db.Recipes.SingleAsync(x => x.Id == entity.RecipeId, cancellationToken)), null);
        entity.Status = ManufacturingStatusCodes.Released;
        entity.ReleasedAt = DateTimeOffset.UtcNow;
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = "Manufacturing.ProductionOrderReleased.v1",
            Content = JsonSerializer.Serialize(new { eventId = entity.Id, schemaVersion = 1, occurredAt = entity.ReleasedAt, correlationId = entity.Id, facilityId = "default", productionOrderId = entity.Id, tenantKey, productSku = entity.ProductSku, recipeId = entity.RecipeId, recipeVersion = entity.RecipeVersion, targetQuantity = entity.TargetQuantity }),
            OccurredOn = entity.ReleasedAt.Value.UtcDateTime, Status = ManufacturingStatusCodes.Pending
        });
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(entity, await db.Recipes.SingleAsync(x => x.Id == entity.RecipeId, cancellationToken)), null);
    }

    public async Task<(ProductionOrderDto? Order, string? Error)> CancelOrderAsync(string tenantKey, Guid orderId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken); var entity = await db.ProductionOrders.SingleOrDefaultAsync(x => x.Id == orderId && x.TenantKey == tenantKey, cancellationToken);
        if (entity is null) return (null, ManufacturingErrorCodes.ProductionOrderNotFound);
        if (entity.Status is ManufacturingStatusCodes.Completed or ManufacturingStatusCodes.Cancelled) return (null, "production_order_not_cancellable");
        entity.Status = ManufacturingStatusCodes.Cancelled; await db.SaveChangesAsync(cancellationToken); return (ToDto(entity, await db.Recipes.SingleAsync(x => x.Id == entity.RecipeId, cancellationToken)), null);
    }

    public IReadOnlyList<ProductionOrderDto> GetOrders(string tenantKey, string? status, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.ProductionOrders.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        // Materialize the order page before resolving recipes. Querying the
        // same DbContext while the orders reader is still open causes
        // Npgsql's "A command is already in progress" failure on PostgreSQL.
        var orders = query
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .ToList();
        var recipeIds = orders.Select(x => x.RecipeId).Distinct().ToArray();
        var recipes = db.Recipes
            .AsNoTracking()
            .Where(recipe => recipeIds.Contains(recipe.Id))
            .ToDictionary(recipe => recipe.Id);
        return orders
            .Select(order => ToDto(order, recipes[order.RecipeId]))
            .ToList();
    }

    public async Task<(ProductionBatchDto? Batch, string? Error)> CreateBatchAsync(string tenantKey, CreateProductionBatchRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var order = await db.ProductionOrders.SingleOrDefaultAsync(x => x.Id == request.ProductionOrderId, cancellationToken);
        if (order is null) return (null, ManufacturingErrorCodes.ProductionOrderNotFound);
        if (!order.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantMismatch);
        var plannedQuantity = request.PlannedQuantity ?? order.TargetQuantity;
        var batchPolicyError = ProductionPolicy.ValidateBatch(new ProductionBatchValidationInput(order.Status, request.BatchNumber, plannedQuantity));
        if (batchPolicyError is not null) return (null, batchPolicyError);
        if (await db.ProductionBatches.AnyAsync(x => x.TenantKey == tenantKey && x.BatchNumber == request.BatchNumber.Trim(), cancellationToken)) return (null, ManufacturingErrorCodes.ProductionBatchExists);
        var entity = new ManufacturingProductionBatchEntity
        {
            Id = Guid.NewGuid(), TenantKey = tenantKey, ProductionOrderId = order.Id, BatchNumber = request.BatchNumber.Trim(),
            Status = ManufacturingStatusCodes.Created, PlannedQuantity = plannedQuantity, ActualOutputQuantity = 0,
            MachineId = request.MachineId, CreatedAt = DateTimeOffset.UtcNow
        };
        if (entity.MachineId.HasValue)
        {
            var machine = await db.Machines.SingleOrDefaultAsync(x => x.Id == entity.MachineId.Value, cancellationToken);
            if (machine is null) return (null, ManufacturingErrorCodes.MachineNotFound);
            if (!machine.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase) || !machine.Active) return (null, ManufacturingErrorCodes.MachineUnavailable);
        }
        if (request.Inputs is { Count: > 0 })
        {
            if (request.Inputs.GroupBy(x => x.LotId).Any(x => x.Count() > 1)) return (null, ManufacturingErrorCodes.InputReservationMismatch);
            foreach (var input in request.Inputs)
            {
                var lot = await db.Lots.SingleOrDefaultAsync(x => x.Id == input.LotId, cancellationToken);
                if (lot is null) return (null, "input_lot_not_found");
                if (!lot.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantMismatch);
                if (!lot.Disposition.Equals(ManufacturingStatusCodes.Released, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.InputLotNotReleased);
                var reservation = await db.LotReservations.SingleOrDefaultAsync(x => x.Id == input.ReservationId && x.LotId == input.LotId, cancellationToken);
                if (reservation is null || !reservation.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.InputReservationMismatch);
                if (reservation.Status != "Reserved" || input.Quantity > reservation.Quantity) return (null, ManufacturingErrorCodes.InputReservationUnavailable);
            }
        }
        db.ProductionBatches.Add(entity);
        if (request.Inputs is { Count: > 0 })
        {
            db.ProductionBatchInputs.AddRange(request.Inputs.Select(input => new ManufacturingProductionBatchInputEntity
            {
                Id = Guid.NewGuid(), ProductionBatchId = entity.Id, LotId = input.LotId, ReservationId = input.ReservationId, Quantity = input.Quantity
            }));
        }
        EntityStatusHistoryStore.Append(db, "production-batch", entity.Id, tenantKey, "", ManufacturingStatusCodes.Created, "system", entity.CreatedAt);
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(entity, [], request.Inputs?.Select(x => new ManufacturingProductionBatchInputEntity { LotId = x.LotId, ReservationId = x.ReservationId, Quantity = x.Quantity }) ?? []), null);
    }

    public IReadOnlyList<ProductionBatchDto> GetBatches(string tenantKey, string? status, int limit)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.ProductionBatches.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var batches = query.OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(limit, 1, 200)).ToList();
        var ids = batches.Select(x => x.Id).ToArray();
        var operations = db.OperationExecutions.AsNoTracking().Where(x => ids.Contains(x.ProductionBatchId)).ToList();
        var inputs = db.ProductionBatchInputs.AsNoTracking().Where(x => ids.Contains(x.ProductionBatchId)).ToList();
        return batches.Select(x => ToDto(x, operations.Where(o => o.ProductionBatchId == x.Id), inputs.Where(i => i.ProductionBatchId == x.Id))).ToList();
    }

    public async Task<(ProductionBatchDto? Batch, string? Error)> ChangeBatchStatusAsync(string tenantKey, Guid batchId, string targetStatus, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.ProductionBatches.SingleOrDefaultAsync(x => x.Id == batchId, cancellationToken);
        if (entity is null) return (null, ManufacturingErrorCodes.ProductionBatchNotFound);
        if (!entity.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantMismatch);
        var order = await db.ProductionOrders.SingleAsync(x => x.Id == entity.ProductionOrderId, cancellationToken);
        var machineAvailable = entity.MachineId is null;
        if (targetStatus == ManufacturingStatusCodes.Started && entity.MachineId is { } machineId)
        {
            var machine = await db.Machines.SingleOrDefaultAsync(x => x.Id == machineId, cancellationToken);
            machineAvailable = machine is not null && machine.Active && machine.Status.Equals("Available", StringComparison.OrdinalIgnoreCase);
        }
        var operations = await db.OperationExecutions.Where(x => x.ProductionBatchId == batchId).ToListAsync(cancellationToken);
        if (targetStatus == ManufacturingStatusCodes.Completed)
        {
            var recipe = await db.Recipes.SingleOrDefaultAsync(x => x.Id == order.RecipeId, cancellationToken);
            var reviewedOperationIds = await db.LossReviews
                .Where(x => x.TenantKey == tenantKey && x.ProductionBatchId == batchId && x.Decision == ManufacturingStatusCodes.Approved)
                .Select(x => x.OperationExecutionId)
                .ToListAsync(cancellationToken);
            var reviewedOperationIdSet = reviewedOperationIds.ToHashSet();
            var hasUnreviewedLoss = recipe is not null && operations.Any(operation =>
                operation.InputQuantity > 0 &&
                operation.OutputQuantity / operation.InputQuantity * 100 < recipe.TargetYieldPercent &&
                !reviewedOperationIdSet.Contains(operation.Id));
            if (hasUnreviewedLoss) return (null, "loss_review_required");
        }
        var transitionError = ProductionPolicy.ValidateTransition(new BatchTransitionValidationInput(
            entity.Status,
            targetStatus,
            operations.Count > 0,
            operations.Where(x => x.Required).All(x => x.Status == ManufacturingStatusCodes.Completed),
            operations.Where(x => x.Required).All(x => x.Status == ManufacturingStatusCodes.Completed && x.QcStatus == "Pass"),
            machineAvailable));
        if (transitionError is not null) return (null, transitionError);
        var previousStatus = entity.Status;
        entity.Status = targetStatus;
        if (targetStatus == ManufacturingStatusCodes.Started) order.Status = "InProgress";
        if (targetStatus == ManufacturingStatusCodes.Completed) order.Status = ManufacturingStatusCodes.Completed;
        if (targetStatus == ManufacturingStatusCodes.Started && entity.StartedAt is null) entity.StartedAt = DateTimeOffset.UtcNow;
        if (targetStatus == ManufacturingStatusCodes.Completed)
        {
            entity.CompletedAt = DateTimeOffset.UtcNow;
            if (entity.OutputLotId is null)
            {
                var outputLot = new ManufacturingLotEntity
                {
                    Id = Guid.NewGuid(), TenantKey = tenantKey, Sku = order.ProductSku,
                    Quantity = entity.ActualOutputQuantity, Uom = order.OutputUom, Disposition = "Quarantined",
                    LotCode = $"LOT-{entity.CompletedAt.Value:yyyyMMdd}-{Guid.NewGuid():N}", LotType = "FinishedGood",
                    QualityStatus = ManufacturingStatusCodes.Pending, CreatedBy = "system", CreatedAt = entity.CompletedAt.Value
                };
                var batchInputs = await db.ProductionBatchInputs.Where(x => x.ProductionBatchId == entity.Id).ToListAsync(cancellationToken);
                var inputLots = batchInputs.Count == 0 ? new List<ManufacturingLotEntity>() : await db.Lots.Where(x => batchInputs.Select(i => i.LotId).Contains(x.Id)).ToListAsync(cancellationToken);
                var reservedInputQuantity = batchInputs.Sum(x => x.Quantity);
                if (reservedInputQuantity > 0 && entity.ActualOutputQuantity > reservedInputQuantity)
                    return (null, ManufacturingErrorCodes.InputQuantityInsufficient);
                foreach (var batchInput in batchInputs)
                {
                    var lot = inputLots.Single(x => x.Id == batchInput.LotId);
                    var reservation = await db.LotReservations.SingleAsync(x => x.Id == batchInput.ReservationId, cancellationToken);
                    if (reservation.Status != "Reserved" || batchInput.Quantity > lot.Quantity) return (null, ManufacturingErrorCodes.InputReservationUnavailable);
                    lot.Quantity -= batchInput.Quantity;
                    reservation.Status = "Consumed";
                }
                var inputQuantity = batchInputs.Sum(x => x.Quantity);
                var transformation = new ManufacturingTransformationEntity
                {
                    Id = Guid.NewGuid(), TenantKey = tenantKey, ProcessStep = $"production-batch:{entity.BatchNumber}",
                    OutputLotId = outputLot.Id, RecipeId = order.RecipeId, MachineId = entity.MachineId,
                    InputQuantity = inputQuantity == 0 ? operations.Sum(x => x.InputQuantity) : inputQuantity, OutputQuantity = entity.ActualOutputQuantity,
                    YieldPercent = (inputQuantity == 0 ? operations.Sum(x => x.InputQuantity) : inputQuantity) == 0 ? 0 : decimal.Round(entity.ActualOutputQuantity / (inputQuantity == 0 ? operations.Sum(x => x.InputQuantity) : inputQuantity) * 100, 2),
                    LossQuantity = (inputQuantity == 0 ? operations.Sum(x => x.InputQuantity) : inputQuantity) - entity.ActualOutputQuantity, CreatedAt = entity.CompletedAt.Value,
                    Inputs = batchInputs.Select(x => new ManufacturingTransformationInputEntity { LotId = x.LotId, Quantity = x.Quantity }).ToList()
                };
                db.Lots.Add(outputLot);
                db.Transformations.Add(transformation);
                db.QualityInspections.Add(new ManufacturingQualityInspectionEntity
                {
                    Id = Guid.NewGuid(), LotId = outputLot.Id, TenantKey = tenantKey, Status = ManufacturingStatusCodes.Pending,
                    MoisturePercent = 0, Inspector = "system:production", Notes = "Awaiting finished-goods quality inspection",
                    InspectedAt = outputLot.CreatedAt
                });
                db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
                {
                    Id = Guid.NewGuid(), TenantKey = tenantKey, LotId = outputLot.Id, TransactionType = "Produce",
                    Quantity = outputLot.Quantity, Uom = outputLot.Uom, FacilityId = "default", StockStatus = outputLot.Disposition,
                    CorrelationId = transformation.Id, OccurredAt = outputLot.CreatedAt
                });
                foreach (var batchInput in batchInputs)
                {
                    var lot = inputLots.Single(x => x.Id == batchInput.LotId);
                    db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
                    {
                        Id = Guid.NewGuid(), TenantKey = tenantKey, LotId = lot.Id, TransactionType = "Issue",
                        Quantity = batchInput.Quantity, Uom = lot.Uom, FacilityId = "default", StockStatus = lot.Disposition,
                        CorrelationId = transformation.Id, OccurredAt = transformation.CreatedAt
                    });
                }
                entity.OutputLotId = outputLot.Id;
                db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
                {
                    Id = Guid.NewGuid(), Type = "Manufacturing.ProductionOutputLotCreated.v1",
                    Content = JsonSerializer.Serialize(new { eventId = transformation.Id, schemaVersion = 1, occurredAt = outputLot.CreatedAt, correlationId = entity.Id, facilityId = "default", productionBatchId = entity.Id, transformationId = transformation.Id, outputLotId = outputLot.Id, tenantKey, productSku = order.ProductSku, quantity = outputLot.Quantity, uom = outputLot.Uom }),
                    OccurredOn = outputLot.CreatedAt.UtcDateTime, Status = ManufacturingStatusCodes.Pending
                });
            }
        }
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = $"Manufacturing.ProductionBatch{targetStatus}.v1",
            Content = JsonSerializer.Serialize(new { eventId = entity.Id, schemaVersion = 1, occurredAt = DateTimeOffset.UtcNow, correlationId = entity.Id, facilityId = "default", productionBatchId = entity.Id, tenantKey, status = targetStatus }),
            OccurredOn = DateTime.UtcNow, Status = ManufacturingStatusCodes.Pending
        });
        EntityStatusHistoryStore.Append(db, "production-batch", entity.Id, tenantKey, previousStatus, targetStatus, "system", DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(entity, await db.OperationExecutions.AsNoTracking().Where(x => x.ProductionBatchId == entity.Id).ToListAsync(cancellationToken), await db.ProductionBatchInputs.AsNoTracking().Where(x => x.ProductionBatchId == entity.Id).ToListAsync(cancellationToken)), null);
    }

    public async Task<(ProductionBatchDto? Batch, string? Error)> CancelBatchAsync(string tenantKey, Guid batchId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken); var entity = await db.ProductionBatches.SingleOrDefaultAsync(x => x.Id == batchId && x.TenantKey == tenantKey, cancellationToken);
        if (entity is null) return (null, ManufacturingErrorCodes.ProductionBatchNotFound);
        if (entity.Status is ManufacturingStatusCodes.Completed or ManufacturingStatusCodes.Cancelled) return (null, ManufacturingErrorCodes.ProductionBatchNotCancellable);
        var previousStatus = entity.Status;
        entity.Status = ManufacturingStatusCodes.Cancelled;
        EntityStatusHistoryStore.Append(db, "production-batch", entity.Id, tenantKey, previousStatus, ManufacturingStatusCodes.Cancelled, "system", DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken); var operations = await db.OperationExecutions.Where(x => x.ProductionBatchId == entity.Id).ToListAsync(cancellationToken); var inputs = await db.ProductionBatchInputs.Where(x => x.ProductionBatchId == entity.Id).ToListAsync(cancellationToken); return (ToDto(entity, operations, inputs), null);
    }

    public async Task<(OperationExecutionDto? Operation, string? Error)> RecordOperationAsync(string tenantKey, Guid batchId, RecordOperationRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var batch = await db.ProductionBatches.SingleOrDefaultAsync(x => x.Id == batchId, cancellationToken);
        if (batch is null) return (null, ManufacturingErrorCodes.ProductionBatchNotFound);
        if (!batch.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantMismatch);
        var order = await db.ProductionOrders.SingleOrDefaultAsync(x => x.Id == batch.ProductionOrderId, cancellationToken);
        var recipe = order is null ? null : await db.Recipes.SingleOrDefaultAsync(x => x.Id == order.RecipeId, cancellationToken);
        var operationPolicyError = ProductionPolicy.ValidateOperation(
            new ProductionBatchValidationInput(batch.Status, "batch", batch.PlannedQuantity),
            new OperationMeasurementValidationInput(request.Sequence, request.ProcessStep, request.Operator, request.InputQuantity, request.OutputQuantity, request.QcStatus));
        if (operationPolicyError is not null) return (null, operationPolicyError);
        if (await db.OperationExecutions.AnyAsync(x => x.ProductionBatchId == batchId && x.Sequence == request.Sequence, cancellationToken)) return (null, ManufacturingErrorCodes.OperationSequenceExists);
        var entity = new ManufacturingOperationExecutionEntity
        {
            Id = Guid.NewGuid(), ProductionBatchId = batchId, Sequence = request.Sequence, ProcessStep = request.ProcessStep.Trim(),
            Operator = request.Operator.Trim(), InputQuantity = request.InputQuantity, OutputQuantity = request.OutputQuantity,
            LossQuantity = request.InputQuantity - request.OutputQuantity, Status = ManufacturingStatusCodes.Completed, Required = request.Required,
            QcStatus = request.QcStatus.Trim(), StartedAt = request.StartedAt ?? DateTimeOffset.UtcNow, CompletedAt = DateTimeOffset.UtcNow
        };
        batch.ActualOutputQuantity += entity.OutputQuantity;
        db.OperationExecutions.Add(entity);
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = "Manufacturing.OperationMeasurementRecorded.v1",
            Content = JsonSerializer.Serialize(new { eventId = entity.Id, schemaVersion = 1, occurredAt = entity.CompletedAt, correlationId = entity.Id, facilityId = "default", productionBatchId = batchId, operationId = entity.Id, processStep = entity.ProcessStep, inputQuantity = entity.InputQuantity, outputQuantity = entity.OutputQuantity, lossQuantity = entity.LossQuantity, qcStatus = entity.QcStatus, tenantKey }),
            OccurredOn = entity.CompletedAt.Value.UtcDateTime, Status = ManufacturingStatusCodes.Pending
        });
        var operationYield = entity.InputQuantity == 0 ? 0 : entity.OutputQuantity / entity.InputQuantity * 100;
        if (recipe is not null && operationYield < recipe.TargetYieldPercent)
        {
            db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
            {
                Id = Guid.NewGuid(), Type = "Manufacturing.LossThresholdExceeded.v1",
                Content = JsonSerializer.Serialize(new { eventId = entity.Id, schemaVersion = 1, occurredAt = entity.CompletedAt, correlationId = entity.Id, productionBatchId = batchId, operationId = entity.Id, tenantKey, targetYieldPercent = recipe.TargetYieldPercent, actualYieldPercent = decimal.Round(operationYield, 2), lossQuantity = entity.LossQuantity, requiresSupervisorReview = true }),
                OccurredOn = entity.CompletedAt.Value.UtcDateTime, Status = ManufacturingStatusCodes.Pending
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(entity), null);
    }

    public IReadOnlyList<EntityStatusHistoryDto> GetBatchStatusHistory(string tenantKey, Guid batchId)
    {
        using var db = dbFactory.CreateDbContext();
        var entity = db.ProductionBatches.AsNoTracking().SingleOrDefault(x => x.Id == batchId);
        if (entity is null || !entity.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase))
            return [];

        var persisted = EntityStatusHistoryStore.Get(db, tenantKey, "production-batch", batchId);
        if (persisted.Count > 0)
            return persisted;

        return ManufacturingStatusHistoryBuilder.ForProductionBatch(
            entity.Id,
            entity.TenantKey,
            entity.Status,
            entity.CreatedAt,
            entity.StartedAt,
            entity.CompletedAt);
    }

    private static ProductionOrderDto ToDto(ManufacturingProductionOrderEntity x, ManufacturingRecipeEntity recipe) => new(x.Id, x.TenantKey, x.OrderNumber, x.ProductSku, x.RecipeId, x.RecipeVersion, x.TargetQuantity, x.OutputUom, x.Status, x.CreatedAt, x.ReleasedAt);
    private static ProductionBatchDto ToDto(ManufacturingProductionBatchEntity x, IEnumerable<ManufacturingOperationExecutionEntity> operations, IEnumerable<ManufacturingProductionBatchInputEntity> inputs) => new(x.Id, x.TenantKey, x.ProductionOrderId, x.BatchNumber, x.Status, x.PlannedQuantity, x.ActualOutputQuantity, x.MachineId, x.OutputLotId, x.CreatedAt, x.StartedAt, x.CompletedAt, operations.Select(ToDto).ToList(), inputs.Select(i => new ProductionInputDto(i.LotId, i.ReservationId, i.Quantity)).ToList());
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
    public string Status { get; set; } = ManufacturingStatusCodes.Created;
    public decimal PlannedQuantity { get; set; }
    public decimal ActualOutputQuantity { get; set; }
    public Guid? MachineId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? OutputLotId { get; set; }
}

public sealed class ManufacturingProductionBatchCostEntity
{
    public Guid Id { get; set; }
    public Guid ProductionBatchId { get; set; }
    public string TenantKey { get; set; } = "";
    public decimal MaterialCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal OverheadCost { get; set; }
    public decimal LossCost { get; set; }
    public decimal TotalCost { get; set; }
    public decimal CostPerOutputUnit { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTimeOffset CalculatedAt { get; set; }
    public string? CalculatedBy { get; set; }
}

public sealed class ManufacturingProductionBatchInputEntity
{
    public Guid Id { get; set; }
    public Guid ProductionBatchId { get; set; }
    public Guid LotId { get; set; }
    public Guid ReservationId { get; set; }
    public decimal Quantity { get; set; }
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
    public string Status { get; set; } = ManufacturingStatusCodes.Completed;
    public bool Required { get; set; } = true;
    public string QcStatus { get; set; } = ManufacturingStatusCodes.Pending;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
