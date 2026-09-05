using System.Text.Json;
using His.Hope.ManufacturingService.Domain;
using Microsoft.EntityFrameworkCore;

public sealed partial class PostgresManufacturingStore
{
    public bool LotExists(Guid lotId)
    {
        using var db = dbFactory.CreateDbContext();
        return db.Lots.Any(x => x.Id == lotId);
    }

    public async Task<LotDto> CreateLotAsync(CreateLotRequest request, CancellationToken cancellationToken = default)
    {
        var lotCode = string.IsNullOrWhiteSpace(request.LotCode)
            ? $"LOT-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"
            : request.LotCode.Trim().ToUpperInvariant();
        var traceabilityError = LotTraceabilityPolicy.Validate(new LotTraceabilityProfile(
            lotCode, request.LotType.Trim(), request.OriginCountryCode?.Trim().ToUpperInvariant(), request.ManufacturedOn,
            request.BestBefore, request.FacilityCode?.Trim(), request.StorageLocationCode?.Trim()));
        if (traceabilityError is not null) throw new InvalidOperationException(traceabilityError);
        var entity = new ManufacturingLotEntity
        {
            Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), Sku = request.Sku.Trim(),
            Quantity = request.Quantity, Uom = request.Uom.Trim(), Disposition = request.Disposition.Trim(),
            BestBefore = request.BestBefore, LotCode = lotCode, LotType = request.LotType.Trim(),
            OriginCountryCode = request.OriginCountryCode?.Trim().ToUpperInvariant(), ManufacturedOn = request.ManufacturedOn,
            FacilityCode = request.FacilityCode?.Trim(), StorageLocationCode = request.StorageLocationCode?.Trim(),
            CertificateOfAnalysisReference = request.CertificateOfAnalysisReference?.Trim(), SourceLotCode = request.SourceLotCode?.Trim(),
            QualityStatus = request.Disposition.Equals(ManufacturingStatusCodes.Released, StringComparison.OrdinalIgnoreCase) ? "Passed" : ManufacturingStatusCodes.Pending,
            CreatedBy = request.RecordedBy?.Trim() ?? "system", CreatedAt = DateTimeOffset.UtcNow
        };
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.Lots.Add(entity);
        db.InventoryTransactions.Add(new ManufacturingInventoryTransactionEntity
        {
            Id = Guid.NewGuid(), TenantKey = entity.TenantKey, LotId = entity.Id, TransactionType = "Receipt",
            Quantity = entity.Quantity, Uom = entity.Uom, FacilityId = "default", StockStatus = entity.Disposition,
            CorrelationId = entity.Id, OccurredAt = entity.CreatedAt
        });
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<(TransformationDto? Transformation, string? Error)> CreateTransformationAsync(CreateTransformationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Inputs.GroupBy(x => x.LotId).Any(x => x.Count() > 1)) return (null, ManufacturingErrorCodes.DuplicateInputLot);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        ManufacturingRecipeEntity? recipe = null;
        ManufacturingMachineEntity? machine = null;
        if (request.RecipeId.HasValue)
        {
            recipe = await db.Recipes.SingleOrDefaultAsync(x => x.Id == request.RecipeId.Value, cancellationToken);
            if (recipe is null) return (null, ManufacturingErrorCodes.RecipeNotFound);
            if (!recipe.Active || !recipe.Status.Equals(ManufacturingStatusCodes.Approved, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.RecipeUnavailable);
            if (!recipe.TenantKey.Equals(request.TenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "recipe_tenant_mismatch");
            if (!recipe.ProductSku.Equals(request.OutputSku, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.RecipeProductMismatch);
            if (!recipe.ProcessStep.Equals(request.ProcessStep, StringComparison.OrdinalIgnoreCase)) return (null, "recipe_process_step_mismatch");
        }
        if (request.MachineId.HasValue)
        {
            machine = await db.Machines.SingleOrDefaultAsync(x => x.Id == request.MachineId.Value, cancellationToken);
            if (machine is null) return (null, ManufacturingErrorCodes.MachineNotFound);
            if (!machine.Active) return (null, "machine_inactive");
            if (!machine.TenantKey.Equals(request.TenantKey, StringComparison.OrdinalIgnoreCase)) return (null, "machine_tenant_mismatch");
        }
        var inputIds = request.Inputs.Select(x => x.LotId).ToArray();
        var lots = await db.Lots.Where(x => inputIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var inputs = new List<(ManufacturingLotEntity Lot, decimal Quantity, ManufacturingLotReservationEntity? Reservation)>();
        var reservationNow = DateTimeOffset.UtcNow;
        foreach (var input in request.Inputs)
        {
            if (!lots.TryGetValue(input.LotId, out var lot)) return (null, "input_lot_not_found");
            if (!lot.TenantKey.Equals(request.TenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantMismatch);
            if (!lot.Disposition.Equals(ManufacturingStatusCodes.Released, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.InputLotNotReleased);
            ManufacturingLotReservationEntity? reservation = null;
            if (input.ReservationId.HasValue)
            {
                reservation = await db.LotReservations.SingleOrDefaultAsync(x => x.Id == input.ReservationId.Value, cancellationToken);
                if (reservation is null) return (null, ManufacturingErrorCodes.ReservationNotFound);
                if (!reservation.TenantKey.Equals(request.TenantKey, StringComparison.OrdinalIgnoreCase) || reservation.LotId != lot.Id) return (null, ManufacturingErrorCodes.ReservationMismatch);
                if (reservation.Status != "Reserved" || input.Quantity > reservation.Quantity) return (null, ManufacturingErrorCodes.ReservationUnavailable);
            }
            var reservedByOther = await db.LotReservations.Where(x => x.LotId == lot.Id && x.Status == "Reserved" && (x.ExpiresAt == null || x.ExpiresAt > reservationNow) && (!input.ReservationId.HasValue || x.Id != input.ReservationId.Value)).SumAsync(x => (decimal?)x.Quantity, cancellationToken) ?? 0;
            if (input.Quantity <= 0 || input.Quantity + reservedByOther > lot.Quantity) return (null, "input_quantity_exceeds_available");
            inputs.Add((lot, input.Quantity, (ManufacturingLotReservationEntity?)reservation));
        }

        var inputQuantity = inputs.Sum(x => x.Quantity);
        if (request.OutputQuantity > inputQuantity)
            return (null, ManufacturingErrorCodes.OutputQuantityExceedsInput);

        foreach (var (lot, quantity, reservation) in inputs)
        {
            lot.Quantity -= quantity;
            if (reservation is not null) reservation.Status = "Consumed";
        }
        var output = new ManufacturingLotEntity
        {
            Id = Guid.NewGuid(), TenantKey = request.TenantKey.Trim(), Sku = request.OutputSku.Trim(),
            Quantity = request.OutputQuantity, Uom = request.OutputUom.Trim(), Disposition = ManufacturingStatusCodes.Released,
            LotCode = $"LOT-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}", LotType = "WorkInProgress",
            QualityStatus = ManufacturingStatusCodes.Pending, CreatedBy = "system", CreatedAt = DateTimeOffset.UtcNow
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
            Status = ManufacturingStatusCodes.Pending
        });
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(transformation, inputs.Select(x => new TransformationInput(x.Lot.Id, x.Quantity, x.Reservation?.Id)).ToList(), ToDto(output)), null);
    }

}
