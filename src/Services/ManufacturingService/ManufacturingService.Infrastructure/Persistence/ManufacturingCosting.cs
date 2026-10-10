using Microsoft.EntityFrameworkCore;
using His.Hope.ManufacturingService.Application;

public sealed partial class PostgresManufacturingStore
{
    public async Task<(ProductionBatchCostDto? Cost, string? Error)> CalculateBatchCostAsync(Guid batchId, string tenantKey, CalculateBatchCostRequest request, CancellationToken cancellationToken = default)
    {
    if (request.LaborCost < 0 || request.OverheadCost < 0 || string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3) return (null, "invalid_batch_cost");
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var batch = await db.ProductionBatches.SingleOrDefaultAsync(x => x.Id == batchId, cancellationToken);
        if (batch is null) return (null, ManufacturingErrorCodes.ProductionBatchNotFound);
        if (!batch.TenantKey.Equals(tenantKey, StringComparison.OrdinalIgnoreCase)) return (null, ManufacturingErrorCodes.TenantScopeDenied);
        var inputs = await db.ProductionBatchInputs.AsNoTracking().Where(x => x.ProductionBatchId == batchId).Join(db.Lots.AsNoTracking(), x => x.LotId, x => x.Id, (input, lot) => new { input.Quantity, lot.Sku }).ToListAsync(cancellationToken);
        var skus = inputs.Select(x => x.Sku).Distinct().ToArray();
    var priceRows = db.PurchaseOrderLines.AsNoTracking()
        .Join(db.PurchaseOrders.AsNoTracking().Where(x => x.TenantKey == tenantKey), line => line.PurchaseOrderId, order => order.Id, (line, _) => line)
        .Where(x => skus.Contains(x.MaterialSku))
        .GroupBy(x => x.MaterialSku)
        .Select(g => new { Sku = g.Key, Quantity = g.Sum(x => x.OrderedQuantity), Value = g.Sum(x => g.Key == x.MaterialSku ? x.OrderedQuantity * x.UnitPrice : 0m) });
    var prices = (await priceRows.ToListAsync(cancellationToken))
        .ToDictionary(x => x.Sku, x => x.Quantity > 0 ? x.Value / x.Quantity : 0m, StringComparer.OrdinalIgnoreCase);
        var materialCost = inputs.Sum(x => x.Quantity * (prices.GetValueOrDefault(x.Sku)));
        var inputQuantity = inputs.Sum(x => x.Quantity);
        var lossQuantity = await db.OperationExecutions.AsNoTracking().Where(x => x.ProductionBatchId == batchId).SumAsync(x => (decimal?)x.LossQuantity, cancellationToken) ?? 0m;
        var lossCost = inputQuantity > 0 ? materialCost * lossQuantity / inputQuantity : 0m;
        var total = materialCost + request.LaborCost + request.OverheadCost;
        var entity = await db.ProductionBatchCosts.SingleOrDefaultAsync(x => x.ProductionBatchId == batchId && x.TenantKey == tenantKey, cancellationToken);
        if (entity is null) { entity = new ManufacturingProductionBatchCostEntity { Id = Guid.NewGuid(), ProductionBatchId = batchId, TenantKey = tenantKey }; db.ProductionBatchCosts.Add(entity); }
        entity.MaterialCost = materialCost; entity.LaborCost = request.LaborCost; entity.OverheadCost = request.OverheadCost; entity.LossCost = lossCost; entity.TotalCost = total; entity.CostPerOutputUnit = batch.ActualOutputQuantity > 0 ? total / batch.ActualOutputQuantity : 0; entity.Currency = request.Currency.Trim().ToUpperInvariant(); entity.CalculatedAt = DateTimeOffset.UtcNow; entity.CalculatedBy = string.IsNullOrWhiteSpace(request.Actor) ? null : request.Actor.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(entity), null);
    }

    public async Task<ProductionBatchCostDto?> GetBatchCostAsync(Guid batchId, string tenantKey, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.ProductionBatchCosts.AsNoTracking().SingleOrDefaultAsync(x => x.ProductionBatchId == batchId && x.TenantKey == tenantKey, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public CostProjectionDto? GetCostProjection(
        string tenantKey,
        string productSku,
        int? recipeVersion,
        decimal plannedQuantity)
    {
        using var db = dbFactory.CreateDbContext();

        var recipes = db.Recipes.AsNoTracking()
            .Include(x => x.Components)
            .Where(x => x.TenantKey == tenantKey && x.ProductSku == productSku && x.Active);
        var recipe = (recipeVersion.HasValue
                ? recipes.Where(x => x.Version == recipeVersion.Value)
                : recipes)
            .OrderByDescending(x => x.Version)
            .FirstOrDefault();
        if (recipe is null) return null;

        var ingredientSkus = recipe.Components.Select(x => x.IngredientSku).Distinct().ToArray();
        var priceRows = db.PurchaseOrderLines.AsNoTracking()
            .Join(db.PurchaseOrders.AsNoTracking().Where(x => x.TenantKey == tenantKey),
                line => line.PurchaseOrderId,
                order => order.Id,
                (line, _) => line)
            .Where(x => ingredientSkus.Contains(x.MaterialSku))
            .ToList();

        var prices = priceRows
            .GroupBy(x => x.MaterialSku)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CostProjectionPrice>)group
                    .Select(x => new CostProjectionPrice(x.OrderedQuantity, x.UnitPrice))
                    .ToList());
        var result = new CostProjectionUseCase().Execute(
            new CostProjectionRequest(plannedQuantity),
            new CostProjectionRecipe(
                recipe.Id,
                recipe.ProductSku,
                recipe.Version,
                recipe.TargetYieldPercent,
                recipe.Components.Select(x => new CostProjectionRecipeComponent(x.IngredientSku, x.Quantity, x.Uom)).ToList()),
            prices.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<His.Hope.ManufacturingService.Application.CostProjectionPrice>)pair.Value
                    .Select(price => new His.Hope.ManufacturingService.Application.CostProjectionPrice(price.OrderedQuantity, price.UnitPrice))
                    .ToList()));
        var components = result.Components
            .Select(x => new CostProjectionComponentDto(x.IngredientSku, x.Uom, x.RequiredInputQuantity, x.EstimatedUnitPrice, x.EstimatedCost, x.HasPrice))
            .ToList();
        return new CostProjectionDto(
            tenantKey,
            recipe.Id,
            recipe.ProductSku,
            recipe.Version,
            recipe.OutputUom,
            plannedQuantity,
            recipe.TargetYieldPercent,
            result.ProjectedLossQuantity,
            result.EstimatedMaterialCost,
            result.EstimatedMaterialCostPerOutputUnit,
            components,
            result.MissingPriceSkus,
            DateTimeOffset.UtcNow);
    }

    private static ProductionBatchCostDto ToDto(ManufacturingProductionBatchCostEntity x) =>
        new(x.Id, x.ProductionBatchId, x.TenantKey, x.MaterialCost, x.LaborCost, x.OverheadCost, x.LossCost, x.TotalCost, x.CostPerOutputUnit, x.Currency, x.CalculatedAt, x.CalculatedBy);
}
