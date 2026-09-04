using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingCapaStore
{
    Task<IReadOnlyList<CapaDto>> GetCapasAsync(string tenantKey, string? status, int limit, CancellationToken cancellationToken);
    Task<(CapaDto? Capa, string? Error)> CreateCapaAsync(string tenantKey, CreateCapaRequest request, string actor, CancellationToken cancellationToken);
    Task<(CapaDto? Capa, string? Error)> UpdateCapaStatusAsync(string tenantKey, Guid capaId, UpdateCapaStatusRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<SupplierEvaluationDto>> GetSupplierEvaluationsAsync(string tenantKey, Guid? supplierId, int limit, CancellationToken cancellationToken);
    Task<(SupplierEvaluationDto? Evaluation, string? Error)> CreateSupplierEvaluationAsync(string tenantKey, CreateSupplierEvaluationRequest request, string actor, CancellationToken cancellationToken);
}
