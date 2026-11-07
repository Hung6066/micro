using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingQualityWorkflowStore
{
    Task<(QualityInspectionDto? Inspection, string? Error)> CreateQualityInspectionAsync(CreateQualityInspectionRequest request, CancellationToken cancellationToken = default);
    Task<(QualitySampleDto? Sample, string? Error)> CreateQualitySampleAsync(CreateQualitySampleRequest request, string tenantKey, CancellationToken cancellationToken = default);
    IReadOnlyList<QualitySampleDto> GetQualitySamples(string tenantKey, Guid? inspectionId, string? disposition, int limit);
    Task<(QualitySampleDto? Sample, string? Error)> ChangeQualitySampleDispositionAsync(Guid sampleId, string tenantKey, QualitySampleDispositionRequest request, CancellationToken cancellationToken = default);
    Task<(InspectionPlanVersionDto? Plan, string? Error)> CreateInspectionPlanVersionAsync(CreateInspectionPlanVersionRequest request, CancellationToken cancellationToken = default);
    IReadOnlyList<InspectionPlanVersionDto> GetInspectionPlanVersions(string tenantKey, string? productSku, string? status, int limit);
    Task<(InspectionPlanVersionDto? Plan, string? Error)> ChangeInspectionPlanLifecycleAsync(Guid planId, string tenantKey, string targetStatus, InspectionPlanLifecycleRequest request, CancellationToken cancellationToken = default);
    Task<(ProductSpecificationDto? Specification, string? Error)> CreateProductSpecificationAsync(CreateProductSpecificationRequest request, CancellationToken cancellationToken = default);
    IReadOnlyList<ProductSpecificationDto> GetProductSpecifications(string tenantKey, string? productSku, string? status, int limit);
    Task<(ProductSpecificationDto? Specification, string? Error)> ChangeProductSpecificationLifecycleAsync(Guid specificationId, string tenantKey, string targetStatus, ProductSpecificationLifecycleRequest request, CancellationToken cancellationToken = default);
    Task<(ManufacturingDeviationDto? Deviation, string? Error)> CreateDeviationAsync(Guid productionBatchId, string tenantKey, CreateDeviationRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ManufacturingDeviationDto>> GetDeviationsAsync(string tenantKey, Guid? productionBatchId, string? status, int limit, CancellationToken cancellationToken = default);
    Task<(ManufacturingDeviationDto? Deviation, string? Error)> ChangeDeviationStatusAsync(Guid deviationId, string tenantKey, string targetStatus, DeviationActionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EntityStatusHistoryDto>> GetDeviationStatusHistoryAsync(string tenantKey, Guid deviationId, CancellationToken cancellationToken = default);
}
