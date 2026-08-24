using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingProductionStore
{
    LotDto CreateLot(CreateLotRequest request);
    (TransformationDto? Transformation, string? Error) CreateTransformation(CreateTransformationRequest request);
    AvailabilityDto GetAvailability(string tenantKey, string sku);
    IReadOnlyList<LotDto> GetLots(string? tenantKey, string? sku, string? disposition, int limit);
    (LotDto? Lot, string? Error) SetLotDisposition(Guid lotId, string disposition, string tenantKey);
    IReadOnlyList<TransformationSummaryDto> GetTransformationSummaries(string? tenantKey, string? processStep, int limit);
    IReadOnlyList<QualityInspectionDto> GetQualityInspections(Guid lotId, string? tenantKey, int limit);
}
