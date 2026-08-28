using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingPlanningWorkflowStore
{
    IReadOnlyList<ManufacturingMaterialRequirementDto> GetMaterialRequirements(string tenantKey, Guid? productionOrderId);
}
