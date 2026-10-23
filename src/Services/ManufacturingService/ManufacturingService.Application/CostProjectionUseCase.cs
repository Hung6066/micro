using His.Hope.ManufacturingService.Domain;

namespace His.Hope.ManufacturingService.Application;

public sealed record CostProjectionRequest(decimal PlannedQuantity);

public sealed record CostProjectionRecipe(
    Guid Id,
    string ProductSku,
    int Version,
    decimal TargetYieldPercent,
    IReadOnlyList<CostProjectionRecipeComponent> Components);

public sealed record CostProjectionRecipeComponent(string IngredientSku, decimal Quantity, string Uom);

public sealed record CostProjectionPrice(decimal OrderedQuantity, decimal UnitPrice);

public sealed record CostProjectionResult(
    decimal ProjectedLossQuantity,
    decimal EstimatedMaterialCost,
    decimal EstimatedMaterialCostPerOutputUnit,
    IReadOnlyList<CostProjectionComponent> Components,
    IReadOnlyList<string> MissingPriceSkus);

public sealed class CostProjectionUseCase
{
    public CostProjectionResult Execute(
        CostProjectionRequest request,
        CostProjectionRecipe recipe,
        IReadOnlyDictionary<string, IReadOnlyList<CostProjectionPrice>> prices)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(prices);

        var breakdown = CostProjectionCalculator.Calculate(
            request.PlannedQuantity,
            recipe.TargetYieldPercent,
            recipe.Components
                .Select(component => new CostProjectionInput(component.IngredientSku, component.Quantity, component.Uom))
                .ToList(),
            prices.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<His.Hope.ManufacturingService.Domain.CostProjectionPrice>)pair.Value
                    .Select(price => new His.Hope.ManufacturingService.Domain.CostProjectionPrice(price.OrderedQuantity, price.UnitPrice))
                    .ToList()));

        return new CostProjectionResult(
            breakdown.ProjectedLossQuantity,
            breakdown.EstimatedMaterialCost,
            breakdown.EstimatedMaterialCostPerOutputUnit,
            breakdown.Components,
            breakdown.MissingPriceSkus);
    }
}
