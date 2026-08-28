using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingWorkflowStore
{
    CrossEntityWorkflowTraceDto? GetCrossEntityWorkflow(string tenantKey, string entityType, Guid entityId);
}
