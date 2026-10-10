using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingWorkflowStore
{
    CrossEntityWorkflowTraceDto? GetCrossEntityWorkflow(string entityType, Guid entityId);

    // Compatibility seam for non-HTTP callers during tenant-context migration.
    CrossEntityWorkflowTraceDto? GetCrossEntityWorkflow(string tenantKey, string entityType, Guid entityId);
}
