using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingPlanningWorkflowStore
{
    IReadOnlyList<ManufacturingMaterialRequirementDto> GetMaterialRequirements(Guid? productionOrderId);

    // Compatibility seam for non-HTTP callers during tenant-context migration.
    IReadOnlyList<ManufacturingMaterialRequirementDto> GetMaterialRequirements(string tenantKey, Guid? productionOrderId);
}
