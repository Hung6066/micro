using Microsoft.EntityFrameworkCore;
using His.Hope.ManufacturingService.Application;

public sealed partial class PostgresManufacturingStore
{
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
}
