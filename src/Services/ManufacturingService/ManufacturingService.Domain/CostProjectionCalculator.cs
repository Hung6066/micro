namespace His.Hope.ManufacturingService.Domain;

public static class CostProjectionCalculator
{
    public static CostProjectionBreakdown Calculate(
        decimal plannedOutputQuantity,
        decimal targetYieldPercent,
        IReadOnlyList<CostProjectionInput> inputs,
        IReadOnlyDictionary<string, IReadOnlyList<CostProjectionPrice>> prices)
    {
        if (plannedOutputQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(plannedOutputQuantity));
        if (targetYieldPercent <= 0 || targetYieldPercent > 100) throw new ArgumentOutOfRangeException(nameof(targetYieldPercent));

        var yieldFraction = targetYieldPercent / 100m;
        var components = inputs.Select(input =>
        {
            var priceRows = prices.TryGetValue(input.IngredientSku, out var rows) ? rows : [];
            var weightedQuantity = priceRows.Sum(x => x.Quantity);
            var unitPrice = weightedQuantity <= 0 ? 0m : priceRows.Sum(x => x.Quantity * x.UnitPrice) / weightedQuantity;
            var requiredQuantity = plannedOutputQuantity * input.Quantity / yieldFraction;
            return new CostProjectionComponent(
                input.IngredientSku,
                input.Uom,
                decimal.Round(requiredQuantity, 3),
                decimal.Round(unitPrice, 4),
                decimal.Round(requiredQuantity * unitPrice, 2),
                priceRows.Count > 0);
        }).ToList();

        var totalCost = components.Sum(x => x.EstimatedCost);
        return new CostProjectionBreakdown(
            decimal.Round(plannedOutputQuantity / yieldFraction - plannedOutputQuantity, 3),
            decimal.Round(totalCost, 2),
            decimal.Round(totalCost / plannedOutputQuantity, 2),
            components,
            components.Where(x => !x.HasPrice).Select(x => x.IngredientSku).Distinct().Order().ToArray());
    }
}

public sealed record CostProjectionInput(string IngredientSku, decimal Quantity, string Uom);
public sealed record CostProjectionPrice(decimal Quantity, decimal UnitPrice);
public sealed record CostProjectionComponent(string IngredientSku, string Uom, decimal RequiredInputQuantity, decimal EstimatedUnitPrice, decimal EstimatedCost, bool HasPrice);
public sealed record CostProjectionBreakdown(decimal ProjectedLossQuantity, decimal EstimatedMaterialCost, decimal EstimatedMaterialCostPerOutputUnit, IReadOnlyList<CostProjectionComponent> Components, IReadOnlyList<string> MissingPriceSkus);
