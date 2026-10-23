using His.Hope.AspNetCore.Tenancy;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.ManufacturingService.Domain;
using Microsoft.EntityFrameworkCore;

public sealed partial class PostgresManufacturingStore
{
    public IReadOnlyList<ManufacturingMaterialRequirementDto> GetMaterialRequirements(Guid? productionOrderId) =>
        GetMaterialRequirements(HisHopeTenantScope.Current ?? throw new InvalidOperationException("Tenant context is required."), productionOrderId);

    public IReadOnlyList<ManufacturingMaterialRequirementDto> GetMaterialRequirements(string tenantKey, Guid? productionOrderId)
    {
        using var db = dbFactory.CreateDbContext();
        var orders = db.ProductionOrders.AsNoTracking().Where(x => x.TenantKey == tenantKey && (x.Status == "Planned" || x.Status == "Open" || x.Status == ManufacturingStatusCodes.Released));
        if (productionOrderId.HasValue) orders = orders.Where(x => x.Id == productionOrderId.Value);
        var orderRows = orders.ToList();
        var recipeIds = orderRows.Select(x => x.RecipeId).Distinct().ToArray();
        var recipes = db.Recipes.AsNoTracking().Include(x => x.Components).Where(x => recipeIds.Contains(x.Id) && x.Status == ManufacturingStatusCodes.Approved).ToDictionary(x => x.Id);
        var skus = recipes.Values.SelectMany(x => x.Components.Select(c => c.IngredientSku)).Distinct().ToArray();
        var lots = db.Lots.AsNoTracking().Where(x => x.TenantKey == tenantKey && skus.Contains(x.Sku) && x.Disposition == ManufacturingStatusCodes.Released).ToList();
        var lotIds = lots.Select(x => x.Id).ToArray();
        var now = DateTimeOffset.UtcNow;
        var reservations = db.LotReservations.AsNoTracking().Where(x => lotIds.Contains(x.LotId) && x.Status == "Reserved" && (x.ExpiresAt == null || x.ExpiresAt > now)).ToList();
        var result = new List<ManufacturingMaterialRequirementDto>();
        foreach (var order in orderRows)
        {
            if (!recipes.TryGetValue(order.RecipeId, out var recipe)) continue;
            foreach (var component in recipe.Components)
            {
                var matchingLots = lots.Where(x => x.Sku.Equals(component.IngredientSku, StringComparison.OrdinalIgnoreCase)).ToList();
                var released = matchingLots.Sum(x => x.Quantity);
                var reserved = reservations.Where(x => matchingLots.Any(l => l.Id == x.LotId)).Sum(x => x.Quantity);
                var required = decimal.Round(order.TargetQuantity * component.Quantity, 3);
                var available = Math.Max(0, released - reserved);
                result.Add(new(tenantKey, order.Id.ToString(), order.OrderNumber, component.IngredientSku, required, released, reserved, available, Math.Max(0, required - available), component.Uom, now));
            }
        }
        return result;
    }
}
