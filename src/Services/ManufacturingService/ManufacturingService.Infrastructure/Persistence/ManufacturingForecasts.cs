using System.Text.Json;
using His.Hope.Contracts.Manufacturing;
using Microsoft.EntityFrameworkCore;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.Persistence.Querying;

public sealed partial class PostgresManufacturingStore : IManufacturingDashboardStore
{
    public SalesForecastDto CreateSalesForecast(string tenantKey, CreateSalesForecastRequest request)
    {
        using var db = dbFactory.CreateDbContext();
        var normalizedTenant = tenantKey.Trim();
        var sku = request.ProductSku.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTenant) || string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(request.Uom) ||
            string.IsNullOrWhiteSpace(request.Source) || string.IsNullOrWhiteSpace(request.Actor) || request.Quantity <= 0 || request.Version <= 0 ||
            request.PeriodStart > request.PeriodEnd)
            throw new InvalidOperationException("invalid_sales_forecast");
        if (db.SalesForecasts.Any(x => x.TenantKey == normalizedTenant && x.ProductSku == sku && x.PeriodStart == request.PeriodStart &&
            x.PeriodEnd == request.PeriodEnd && x.Version == request.Version))
            throw new InvalidOperationException("forecast_version_exists");

        var now = DateTimeOffset.UtcNow;
        var entity = new ManufacturingSalesForecastEntity
        {
            Id = Guid.NewGuid(), TenantKey = normalizedTenant, ProductSku = sku,
            PeriodStart = request.PeriodStart, PeriodEnd = request.PeriodEnd, Quantity = request.Quantity,
            Uom = request.Uom.Trim(), Source = request.Source.Trim(), Actor = request.Actor.Trim(), Version = request.Version, CreatedAt = now
        };
        db.SalesForecasts.Add(entity);
        db.OutboxMessages.Add(new ManufacturingOutboxMessageEntity
        {
            Id = Guid.NewGuid(), Type = "Manufacturing.SalesForecastChanged.v1", Status = "Pending", OccurredOn = now.UtcDateTime,
            Content = JsonSerializer.Serialize(new { eventId = entity.Id, schemaVersion = 1, occurredAt = now, correlationId = entity.Id,
                tenantKey = entity.TenantKey, productSku = entity.ProductSku, periodStart = entity.PeriodStart, periodEnd = entity.PeriodEnd,
                quantity = entity.Quantity, uom = entity.Uom, version = entity.Version, source = entity.Source, actor = entity.Actor })
        });
        db.SaveChanges();
        return ToDto(entity);
    }

    public IReadOnlyList<SalesForecastDto> GetSalesForecasts(string tenantKey, string? productSku, int limit, int page = 1)
    {
        using var db = dbFactory.CreateDbContext();
        var query = db.SalesForecasts.AsNoTracking().Where(x => x.TenantKey == tenantKey);
        if (!string.IsNullOrWhiteSpace(productSku)) query = query.Where(x => x.ProductSku == productSku.Trim());
        return query.TagUseCase("Manufacturing.Planning.GetSalesForecasts")
            .OrderByDescending(x => x.PeriodStart).ThenByDescending(x => x.Version)
            .ApplyPage(page, limit).Select(ToDto).ToList();
    }

    public (IReadOnlyList<SalesForecastMaterialRequirementDto> Requirements, string? Error) GetSalesForecastMaterialRequirements(string tenantKey, Guid forecastId)
    {
        using var db = dbFactory.CreateDbContext();
        var forecast = db.SalesForecasts.AsNoTracking().SingleOrDefault(x => x.Id == forecastId && x.TenantKey == tenantKey);
        if (forecast is null) return ([], "forecast_not_found");
        var recipe = db.Recipes.AsNoTracking().Include(x => x.Components)
            .Where(x => x.TenantKey == tenantKey && x.ProductSku == forecast.ProductSku && x.Status == "Approved" && x.Active)
            .OrderByDescending(x => x.Version).FirstOrDefault();
        if (recipe is null) return ([], "approved_recipe_not_found");

        var skus = recipe.Components.Select(x => x.IngredientSku).Distinct().ToArray();
        var lots = db.Lots.AsNoTracking().Where(x => x.TenantKey == tenantKey && skus.Contains(x.Sku) && x.Disposition == "Released").ToList();
        var lotIds = lots.Select(x => x.Id).ToArray();
        var now = DateTimeOffset.UtcNow;
        var reservations = db.LotReservations.AsNoTracking().Where(x => lotIds.Contains(x.LotId) && x.Status == "Reserved" && (x.ExpiresAt == null || x.ExpiresAt > now)).ToList();
        var result = recipe.Components.Select(component =>
        {
            var matchingLots = lots.Where(x => x.Sku.Equals(component.IngredientSku, StringComparison.OrdinalIgnoreCase)).ToList();
            var released = matchingLots.Sum(x => x.Quantity);
            var reserved = reservations.Where(x => matchingLots.Any(l => l.Id == x.LotId)).Sum(x => x.Quantity);
            var required = decimal.Round(forecast.Quantity * component.Quantity, 3);
            var available = Math.Max(0, released - reserved);
            return new SalesForecastMaterialRequirementDto(forecast.Id, tenantKey, forecast.ProductSku, forecast.PeriodStart, forecast.PeriodEnd,
                component.IngredientSku, required, released, reserved, available, Math.Max(0, required - available), component.Uom, now);
        }).ToList();
        return (result, null);
    }

    private static SalesForecastDto ToDto(ManufacturingSalesForecastEntity x) =>
        new(x.Id, x.TenantKey, x.ProductSku, x.PeriodStart, x.PeriodEnd, x.Quantity, x.Uom, x.Source, x.Actor, x.Version, x.CreatedAt);
}
