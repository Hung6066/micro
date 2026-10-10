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
    Task<(ProductionBatchCostDto? Cost, string? Error)> CalculateBatchCostAsync(Guid batchId, string tenantKey, CalculateBatchCostRequest request, CancellationToken cancellationToken = default);
    Task<ProductionBatchCostDto?> GetBatchCostAsync(Guid batchId, string tenantKey, CancellationToken cancellationToken = default);
    IReadOnlyList<SalesForecastDto> GetSalesForecasts(string tenantKey, string? productSku, int limit, int page = 1);
    Task<SalesForecastDto> CreateSalesForecastAsync(string tenantKey, CreateSalesForecastRequest request, CancellationToken cancellationToken = default);
    (IReadOnlyList<SalesForecastMaterialRequirementDto> Requirements, string? Error) GetSalesForecastMaterialRequirements(string tenantKey, Guid forecastId);
}
