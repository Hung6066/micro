using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingProductionOrderStore
{
    (ProductionOrderDto? Order, string? Error) CreateOrder(string tenantKey, CreateProductionOrderRequest request);
    (ProductionOrderDto? Order, string? Error) ReleaseOrder(string tenantKey, Guid orderId);
    (ProductionOrderDto? Order, string? Error) CancelOrder(string tenantKey, Guid orderId);
    IReadOnlyList<ProductionOrderDto> GetOrders(string tenantKey, string? status, int limit);
    (ProductionBatchDto? Batch, string? Error) CreateBatch(string tenantKey, CreateProductionBatchRequest request);
    IReadOnlyList<ProductionBatchDto> GetBatches(string tenantKey, string? status, int limit);
    (ProductionBatchDto? Batch, string? Error) ChangeBatchStatus(string tenantKey, Guid batchId, string targetStatus);
    (ProductionBatchDto? Batch, string? Error) CancelBatch(string tenantKey, Guid batchId);
    (OperationExecutionDto? Operation, string? Error) RecordOperation(string tenantKey, Guid batchId, RecordOperationRequest request);
    IReadOnlyList<EntityStatusHistoryDto> GetBatchStatusHistory(string tenantKey, Guid batchId);
}
