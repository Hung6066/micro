using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingQualityWorkflowStore
{
    (QualityInspectionDto? Inspection, string? Error) CreateQualityInspection(CreateQualityInspectionRequest request);
    (QualitySampleDto? Sample, string? Error) CreateQualitySample(CreateQualitySampleRequest request, string tenantKey);
    IReadOnlyList<QualitySampleDto> GetQualitySamples(string tenantKey, Guid? inspectionId, string? disposition, int limit);
    (QualitySampleDto? Sample, string? Error) ChangeQualitySampleDisposition(Guid sampleId, string tenantKey, QualitySampleDispositionRequest request);
    (InspectionPlanVersionDto? Plan, string? Error) CreateInspectionPlanVersion(CreateInspectionPlanVersionRequest request);
    IReadOnlyList<InspectionPlanVersionDto> GetInspectionPlanVersions(string tenantKey, string? productSku, string? status, int limit);
    (InspectionPlanVersionDto? Plan, string? Error) ChangeInspectionPlanLifecycle(Guid planId, string tenantKey, string targetStatus, InspectionPlanLifecycleRequest request);
    (ProductSpecificationDto? Specification, string? Error) CreateProductSpecification(CreateProductSpecificationRequest request);
    IReadOnlyList<ProductSpecificationDto> GetProductSpecifications(string tenantKey, string? productSku, string? status, int limit);
    (ProductSpecificationDto? Specification, string? Error) ChangeProductSpecificationLifecycle(Guid specificationId, string tenantKey, string targetStatus, ProductSpecificationLifecycleRequest request);
    (ManufacturingDeviationDto? Deviation, string? Error) CreateDeviation(Guid productionBatchId, string tenantKey, CreateDeviationRequest request);
    IReadOnlyList<ManufacturingDeviationDto> GetDeviations(string tenantKey, Guid? productionBatchId, string? status, int limit);
    (ManufacturingDeviationDto? Deviation, string? Error) ChangeDeviationStatus(Guid deviationId, string tenantKey, string targetStatus, DeviationActionRequest request);
    IReadOnlyList<EntityStatusHistoryDto> GetDeviationStatusHistory(string tenantKey, Guid deviationId);
}
