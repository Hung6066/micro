using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingProductionStore
{
    LotDto CreateLot(CreateLotRequest request);
    (TransformationDto? Transformation, string? Error) CreateTransformation(CreateTransformationRequest request);
    AvailabilityDto GetAvailability(string tenantKey, string sku);
    IReadOnlyList<LotDto> GetLots(string? tenantKey, string? sku, string? disposition, int limit);
    IReadOnlyList<LotStatusHistoryDto> GetLotStatusHistory(Guid lotId, string tenantKey, int limit);
    (LotDto? Lot, string? Error) SetLotDisposition(Guid lotId, string disposition, string tenantKey, string? actor = null, string? reasonCode = null, string? evidenceReference = null, DateTimeOffset? expectedUpdatedAt = null);
    IReadOnlyList<TransformationSummaryDto> GetTransformationSummaries(string? tenantKey, string? processStep, int limit);
    IReadOnlyList<QualityInspectionDto> GetQualityInspections(Guid lotId, string? tenantKey, int limit);
}
