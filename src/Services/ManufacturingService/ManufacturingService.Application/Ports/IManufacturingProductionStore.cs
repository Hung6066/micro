using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingProductionStore
{
    LotDto CreateLot(CreateLotRequest request);
    (TransformationDto? Transformation, string? Error) CreateTransformation(CreateTransformationRequest request);
    AvailabilityDto GetAvailability(string tenantKey, string sku);
    Task<IReadOnlyList<LotDto>> GetLotsAsync(string? sku, string? disposition, int limit, int page = 1, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LotDto>> GetLotsAsync(string? tenantKey, string? sku, string? disposition, int limit, int page = 1, CancellationToken cancellationToken = default);
    IReadOnlyList<LotStatusHistoryDto> GetLotStatusHistory(Guid lotId, string tenantKey, int limit, int page = 1);
    (LotDto? Lot, string? Error) SetLotDisposition(Guid lotId, string disposition, string tenantKey, string? actor = null, string? reasonCode = null, string? evidenceReference = null, DateTimeOffset? expectedUpdatedAt = null);
    IReadOnlyList<TransformationSummaryDto> GetTransformationSummaries(string? processStep, int limit, int page = 1);
    // Compatibility seam for callers that still pass a tenant selector.
    IReadOnlyList<TransformationSummaryDto> GetTransformationSummaries(string? tenantKey, string? processStep, int limit, int page = 1);
    Task<IReadOnlyList<QualityInspectionDto>> GetQualityInspectionsAsync(Guid lotId, string? tenantKey, int limit, int page = 1, CancellationToken cancellationToken = default);
}
