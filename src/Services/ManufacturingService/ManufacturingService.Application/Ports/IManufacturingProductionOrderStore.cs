using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingProductionOrderStore
{
    Task<(ProductionOrderDto? Order, string? Error)> CreateOrderAsync(string tenantKey, CreateProductionOrderRequest request, CancellationToken cancellationToken = default);
    Task<(ProductionOrderDto? Order, string? Error)> ReleaseOrderAsync(string tenantKey, Guid orderId, CancellationToken cancellationToken = default);
    Task<(ProductionOrderDto? Order, string? Error)> CancelOrderAsync(string tenantKey, Guid orderId, CancellationToken cancellationToken = default);
    IReadOnlyList<ProductionOrderDto> GetOrders(string tenantKey, string? status, int limit);
    Task<(ProductionBatchDto? Batch, string? Error)> CreateBatchAsync(string tenantKey, CreateProductionBatchRequest request, CancellationToken cancellationToken = default);
    IReadOnlyList<ProductionBatchDto> GetBatches(string tenantKey, string? status, int limit);
    Task<(ProductionBatchDto? Batch, string? Error)> ChangeBatchStatusAsync(string tenantKey, Guid batchId, string targetStatus, CancellationToken cancellationToken = default);
    Task<(ProductionBatchDto? Batch, string? Error)> CancelBatchAsync(string tenantKey, Guid batchId, CancellationToken cancellationToken = default);
    Task<(OperationExecutionDto? Operation, string? Error)> RecordOperationAsync(string tenantKey, Guid batchId, RecordOperationRequest request, CancellationToken cancellationToken = default);
    IReadOnlyList<EntityStatusHistoryDto> GetBatchStatusHistory(string tenantKey, Guid batchId);
}
