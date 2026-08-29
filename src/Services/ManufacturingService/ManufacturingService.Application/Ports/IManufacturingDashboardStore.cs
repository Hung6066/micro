using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingDashboardStore
{
    ManufacturingDashboardSummaryDto GetDashboardSummary(string tenantKey);
    ManufacturingProductionKpiDto GetProductionKpis(string tenantKey);
    ManufacturingMachineHealthDto GetMachineHealth(string tenantKey, int dueWithinDays);
    ManufacturingOeeDto GetOee(string tenantKey, Guid? machineId);
    ManufacturingProductionCostDto GetProductionCosts(string tenantKey);
    IReadOnlyList<ManufacturingExecutiveExceptionDto> GetExecutiveExceptions(string tenantKey, int expiryWithinDays, int downtimeThresholdHours);
    CostProjectionDto? GetCostProjection(string tenantKey, string productSku, int? recipeVersion, decimal plannedQuantity);
    (ProductionBatchCostDto? Cost, string? Error) CalculateBatchCost(Guid batchId, string tenantKey, CalculateBatchCostRequest request);
    ProductionBatchCostDto? GetBatchCost(Guid batchId, string tenantKey);
    IReadOnlyList<SalesForecastDto> GetSalesForecasts(string tenantKey, string? productSku, int limit, int page = 1);
    SalesForecastDto CreateSalesForecast(string tenantKey, CreateSalesForecastRequest request);
    (IReadOnlyList<SalesForecastMaterialRequirementDto> Requirements, string? Error) GetSalesForecastMaterialRequirements(string tenantKey, Guid forecastId);
}
