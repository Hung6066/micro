using Microsoft.EntityFrameworkCore;
using His.Hope.Persistence.Querying;
using System.Text.Json;

public sealed partial class PostgresManufacturingStore
{
    public ManufacturingDashboardSummaryDto GetDashboardSummary(string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var lots = db.Lots.AsNoTracking().Where(x => x.TenantKey == tenantKey).ToList();
        var transformations = db.Transformations.AsNoTracking().Where(x => x.TenantKey == tenantKey).ToList();
        var orders = db.ProductionOrders.AsNoTracking().Where(x => x.TenantKey == tenantKey).ToList();
        var batches = db.ProductionBatches.AsNoTracking().Where(x => x.TenantKey == tenantKey).ToList();
        var inspections = db.QualityInspections.AsNoTracking().Where(x => x.TenantKey == tenantKey).ToList();
        var released = lots.Where(x => x.Disposition == ManufacturingStatusCodes.Released).Sum(x => x.Quantity);
        var quarantined = lots.Where(x => x.Disposition is "Quarantined" or "Quarantine" or "Hold").Sum(x => x.Quantity);
        return new ManufacturingDashboardSummaryDto(
            tenantKey,
            lots.Count,
            released,
            quarantined,
            transformations.Count,
            transformations.Count == 0 ? 0 : decimal.Round(transformations.Average(x => x.YieldPercent), 2),
            transformations.Sum(x => x.LossQuantity),
            orders.Count(x => x.Status is "Planned" or ManufacturingStatusCodes.Released or "InProgress"),
            batches.Count(x => x.Status is ManufacturingStatusCodes.Created or ManufacturingStatusCodes.Started or "Paused"),
            inspections.Count(x => x.Status == ManufacturingStatusCodes.Pending),
            inspections.Count(x => x.Status == "Fail"),
            DateTimeOffset.UtcNow);
    }

    public ManufacturingProductionKpiDto GetProductionKpis(string tenantKey)
    {
        using var db = dbFactory.CreateDbContext();
        var completed = db.ProductionBatches.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.Status == ManufacturingStatusCodes.Completed).ToList();
        if (completed.Count == 0)
            return new ManufacturingProductionKpiDto(tenantKey, 0, 0, 0, 0, 0, 0, 0, 0, DateTimeOffset.UtcNow, "insufficient-data", ["Manufacturing.ProductionBatchCompleted.v1"]);

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
            averageYield, decimal.Round(target, 2), decimal.Round(averageYield - target, 2), DateTimeOffset.UtcNow,
            "complete", ["Manufacturing.ProductionBatchCompleted.v1", "Manufacturing.OperationRecorded.v1"]);
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
            .Where(x => x.TenantKey == tenantKey && x.Status == ManufacturingStatusCodes.Completed && (!machineId.HasValue || x.MachineId == machineId))
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
        var completed = db.ProductionBatches.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.Status == ManufacturingStatusCodes.Completed).ToList();
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
            if (!lot.Disposition.Equals(ManufacturingStatusCodes.Released, StringComparison.OrdinalIgnoreCase))
                exceptions.Add(new("lot_hold", "High", "Lot is not released", $"SKU {lot.Sku} has disposition {lot.Disposition} and is excluded from ATP.", lot.Id, lot.CreatedAt));
            else if (lot.BestBefore is { } bestBefore && bestBefore <= expiryAt)
                exceptions.Add(new("expiry_risk", "Medium", "Lot expiry risk", $"SKU {lot.Sku} expires on {bestBefore:yyyy-MM-dd}.", lot.Id, lot.CreatedAt));
        }
        foreach (var inspection in db.QualityInspections.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.Status == ManufacturingStatusCodes.Pending).ToList())
            exceptions.Add(new(ManufacturingErrorCodes.PendingQuality, "High", "Quality inspection pending", $"Lot {inspection.LotId} is waiting for quality disposition.", inspection.LotId, inspection.InspectedAt));
        foreach (var downtime in db.MachineDowntimes.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.Status == "Open" && x.StartedAt <= now.AddHours(-Math.Clamp(downtimeThresholdHours, 1, 720))).ToList())
            exceptions.Add(new("prolonged_downtime", "High", "Machine downtime prolonged", $"Machine {downtime.MachineId} has been down since {downtime.StartedAt:O}.", downtime.Id, downtime.StartedAt));
        var latestTelemetry = db.MachineTelemetry.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey)
            .ToList()
            .GroupBy(x => x.MachineId)
            .Select(group => group.OrderByDescending(x => x.ObservedAt).ThenByDescending(x => x.ReceivedAt).First());
        foreach (var telemetry in latestTelemetry.Where(x => x.State is "Fault" or "UnplannedDown"))
            exceptions.Add(new("machine_telemetry_fault", "High", "Machine telemetry fault", $"Machine {telemetry.MachineId} reported state {telemetry.State} from {telemetry.Source} at {telemetry.ObservedAt:O}.", telemetry.MachineId, telemetry.ObservedAt));
        foreach (var recipe in db.Recipes.AsNoTracking().Where(x => x.TenantKey == tenantKey && x.Status == ManufacturingStatusCodes.Submitted).ToList())
            exceptions.Add(new("recipe_approval", "Medium", "Recipe approval pending", $"Recipe {recipe.ProductSku} v{recipe.Version} is submitted for approval.", recipe.Id, recipe.CreatedAt));
        var reviewedLossOperationIds = db.LossReviews.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.Decision == ManufacturingStatusCodes.Approved)
            .Select(x => x.OperationExecutionId)
            .ToHashSet();
        foreach (var loss in db.OutboxMessages.AsNoTracking().Where(x => x.Type == "Manufacturing.LossThresholdExceeded.v1" && x.Status == ManufacturingStatusCodes.Pending && x.Content.Contains($"\"tenantKey\":\"{tenantKey}\"")).ToList())
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
}
