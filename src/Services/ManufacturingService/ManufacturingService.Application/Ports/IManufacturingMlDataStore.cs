using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingMlDataStore
{
    (OperationMeasurementDto? Measurement, string? Error) RecordOperationMeasurement(string tenantKey, string actor, RecordOperationMeasurementRequest request);
    IReadOnlyList<OperationMeasurementDto> GetOperationMeasurements(string tenantKey, Guid productionBatchId, int limit);
    (SalesActualDto? Actual, string? Error) RecordSalesActual(string tenantKey, string actor, RecordSalesActualRequest request);
    IReadOnlyList<SalesActualDto> GetSalesActuals(string tenantKey, string? productSku, int limit);
    (MlFeatureSnapshotDto? Snapshot, string? Error) CreateFeatureSnapshot(string tenantKey, string actor, MlFeatureSnapshotRequest request);
    IReadOnlyList<MlFeatureSnapshotDto> GetFeatureSnapshots(string tenantKey, string datasetKey, DateTimeOffset? from, DateTimeOffset? to, int limit);
    MlDatasetQualityDto GetDatasetQuality(string tenantKey, string datasetKey);
}
