using Microsoft.EntityFrameworkCore;
using His.Hope.Contracts.Manufacturing;

public sealed partial class PostgresManufacturingStore
{
    public CrossEntityWorkflowTraceDto? GetCrossEntityWorkflow(string tenantKey, string entityType, Guid entityId)
    {
        using var db = dbFactory.CreateDbContext();
        return entityType.Trim().ToLowerInvariant() switch
        {
            "purchase-order" => BuildCrossEntityFromPurchaseOrder(db, tenantKey, entityId),
            "production-batch" => BuildCrossEntityFromProductionBatch(db, tenantKey, entityId),
            "lot" => BuildCrossEntityFromLot(db, tenantKey, entityId),
            _ => null,
        };
    }

    private static CrossEntityWorkflowTraceDto? BuildCrossEntityFromPurchaseOrder(
        ManufacturingDbContext db, string tenantKey, Guid purchaseOrderId)
    {
        var order = db.PurchaseOrders.AsNoTracking().SingleOrDefault(x => x.Id == purchaseOrderId);
        if (order is null || !order.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase))
            return null;

        var steps = new List<CrossEntityWorkflowStepDto>
        {
            Step("purchase-order", order.Id, order.OrderNumber, order.Status, "/procurement", order.OrderedAt),
        };

        var receipt = db.InboundReceipts.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.PurchaseOrderId == order.Id)
            .OrderBy(x => x.ReceivedAt)
            .FirstOrDefault();
        if (receipt is null)
            return FinalizeTrace("purchase-order", order.Id, steps);

        var lot = db.Lots.AsNoTracking().SingleOrDefault(x => x.Id == receipt.LotId);
        if (lot is not null)
            AppendLotChain(db, tenantKey, lot, steps);

        return FinalizeTrace("purchase-order", order.Id, steps);
    }

    private static CrossEntityWorkflowTraceDto? BuildCrossEntityFromProductionBatch(
        ManufacturingDbContext db, string tenantKey, Guid batchId)
    {
        var batch = db.ProductionBatches.AsNoTracking().SingleOrDefault(x => x.Id == batchId);
        if (batch is null || !batch.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase))
            return null;

        var steps = new List<CrossEntityWorkflowStepDto>();
        var input = db.ProductionBatchInputs.AsNoTracking()
            .Where(x => x.ProductionBatchId == batch.Id)
            .OrderBy(x => x.Quantity)
            .FirstOrDefault();
        if (input is not null)
        {
            var lot = db.Lots.AsNoTracking().SingleOrDefault(x => x.Id == input.LotId);
            if (lot is not null)
            {
                var receipt = db.InboundReceipts.AsNoTracking()
                    .Where(x => x.TenantKey == tenantKey && x.LotId == lot.Id)
                    .OrderBy(x => x.ReceivedAt)
                    .FirstOrDefault();
                if (receipt is not null)
                {
                    var order = db.PurchaseOrders.AsNoTracking().SingleOrDefault(x => x.Id == receipt.PurchaseOrderId);
                    if (order is not null)
                        steps.Add(Step("purchase-order", order.Id, order.OrderNumber, order.Status, "/procurement", order.OrderedAt));
                }

                AppendLotChain(db, tenantKey, lot, steps);
            }
        }

        steps.Add(Step("production-batch", batch.Id, batch.BatchNumber, batch.Status, "/production", batch.StartedAt ?? batch.CreatedAt));

        if (batch.OutputLotId is { } outputLotId)
        {
            var outputLot = db.Lots.AsNoTracking().SingleOrDefault(x => x.Id == outputLotId);
            if (outputLot is not null)
                AppendOutputLotChain(db, tenantKey, outputLot, steps);
        }

        return FinalizeTrace("production-batch", batch.Id, steps);
    }

    private static CrossEntityWorkflowTraceDto? BuildCrossEntityFromLot(
        ManufacturingDbContext db, string tenantKey, Guid lotId)
    {
        var lot = db.Lots.AsNoTracking().SingleOrDefault(x => x.Id == lotId);
        if (lot is null || !lot.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase))
            return null;

        var steps = new List<CrossEntityWorkflowStepDto>();
        var receipt = db.InboundReceipts.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.LotId == lot.Id)
            .OrderBy(x => x.ReceivedAt)
            .FirstOrDefault();
        if (receipt is not null)
        {
            var order = db.PurchaseOrders.AsNoTracking().SingleOrDefault(x => x.Id == receipt.PurchaseOrderId);
            if (order is not null)
                steps.Add(Step("purchase-order", order.Id, order.OrderNumber, order.Status, "/procurement", order.OrderedAt));
        }

        AppendLotChain(db, tenantKey, lot, steps);

        var batchInput = db.ProductionBatchInputs.AsNoTracking()
            .Where(x => x.LotId == lot.Id)
            .OrderByDescending(x => x.Quantity)
            .FirstOrDefault();
        if (batchInput is not null)
        {
            var batch = db.ProductionBatches.AsNoTracking().SingleOrDefault(x => x.Id == batchInput.ProductionBatchId);
            if (batch is not null)
            {
                steps.Add(Step("production-batch", batch.Id, batch.BatchNumber, batch.Status, "/production", batch.StartedAt ?? batch.CreatedAt));
                if (batch.OutputLotId is { } outputLotId)
                {
                    var outputLot = db.Lots.AsNoTracking().SingleOrDefault(x => x.Id == outputLotId);
                    if (outputLot is not null)
                        AppendOutputLotChain(db, tenantKey, outputLot, steps);
                }
            }
        }

        return FinalizeTrace("lot", lot.Id, steps);
    }

    private static void AppendLotChain(
        ManufacturingDbContext db, string tenantKey, ManufacturingLotEntity lot, List<CrossEntityWorkflowStepDto> steps)
    {
        steps.Add(Step("lot", lot.Id, lot.LotCode, lot.Disposition, "/inventory/lots", lot.ReceivedAt ?? lot.CreatedAt));

        var inspection = db.QualityInspections.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.LotId == lot.Id)
            .OrderByDescending(x => x.InspectedAt)
            .FirstOrDefault();
        if (inspection is not null)
            steps.Add(Step("quality-inspection", inspection.Id, lot.Sku, inspection.Status, "/quality-inspections", inspection.InspectedAt));
    }

    private static void AppendOutputLotChain(
        ManufacturingDbContext db, string tenantKey, ManufacturingLotEntity outputLot, List<CrossEntityWorkflowStepDto> steps)
    {
        steps.Add(Step("output-lot", outputLot.Id, outputLot.LotCode, outputLot.Disposition, "/inventory/lots", outputLot.CreatedAt));

        var inspection = db.QualityInspections.AsNoTracking()
            .Where(x => x.TenantKey == tenantKey && x.LotId == outputLot.Id)
            .OrderByDescending(x => x.InspectedAt)
            .FirstOrDefault();
        if (inspection is not null)
            steps.Add(Step("finished-qc", inspection.Id, outputLot.Sku, inspection.Status, "/quality-inspections", inspection.InspectedAt));
    }

    private static CrossEntityWorkflowStepDto Step(
        string key, Guid entityId, string title, string status, string route, DateTimeOffset? occurredAt) =>
        new(key, EntityTypeForKey(key), entityId, title, status, route, "upcoming", occurredAt);

    private static string EntityTypeForKey(string key) => key switch
    {
        "purchase-order" => "purchase-order",
        "production-batch" => "production-batch",
        "quality-inspection" or "finished-qc" => "quality-inspection",
        "output-lot" or "lot" => "lot",
        _ => key,
    };

    private static CrossEntityWorkflowTraceDto FinalizeTrace(
        string anchorEntityType, Guid anchorEntityId, List<CrossEntityWorkflowStepDto> steps)
    {
        var anchorIndex = steps.FindIndex(x =>
            x.EntityType.Equals(anchorEntityType, StringComparison.OrdinalIgnoreCase) && x.EntityId == anchorEntityId);
        if (anchorIndex < 0)
            anchorIndex = 0;

        var lastCompleteIndex = -1;
        for (var i = 0; i < steps.Count; i++)
        {
            if (IsTerminalStatus(steps[i].EntityType, steps[i].Status))
                lastCompleteIndex = i;
            else
                break;
        }

        var resolved = steps.Select((step, index) =>
        {
            var state = ResolveStepState(step.EntityType, step.Status, index, lastCompleteIndex, index == anchorIndex);
            return step with { State = state };
        }).ToList();

        return new CrossEntityWorkflowTraceDto(anchorEntityType, anchorEntityId, resolved);
    }

    private static string ResolveStepState(string entityType, string status, int index, int lastCompleteIndex, bool isAnchor)
    {
        if (status is "Cancelled" or "Rejected" or "Fail")
            return "cancelled";
        if (index <= lastCompleteIndex)
            return "complete";
        if (index == lastCompleteIndex + 1 || (lastCompleteIndex < 0 && index == 0))
            return isAnchor || !IsTerminalStatus(entityType, status) ? "current" : "complete";
        return "upcoming";
    }

    private static bool IsTerminalStatus(string entityType, string status) =>
        (entityType, status) switch
        {
            ("purchase-order", "Approved" or "PartiallyReceived" or "Closed" or "Cancelled") => true,
            ("lot", "Released" or "Rejected") => true,
            ("quality-inspection", "Pass" or "Rejected" or "Fail") => true,
            ("production-batch", "Completed" or "Cancelled") => true,
            _ when status is "Completed" or "Closed" or "Pass" or "Released" => true,
            _ => false,
        };
}
