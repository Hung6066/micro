using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingCapaStore
{
    IReadOnlyList<CapaDto> GetCapas(string tenantKey, string? status, int limit);
    (CapaDto? Capa, string? Error) CreateCapa(string tenantKey, CreateCapaRequest request, string actor);
    (CapaDto? Capa, string? Error) UpdateCapaStatus(string tenantKey, Guid capaId, UpdateCapaStatusRequest request);
    IReadOnlyList<SupplierEvaluationDto> GetSupplierEvaluations(string tenantKey, Guid? supplierId, int limit);
    (SupplierEvaluationDto? Evaluation, string? Error) CreateSupplierEvaluation(string tenantKey, CreateSupplierEvaluationRequest request, string actor);
}
