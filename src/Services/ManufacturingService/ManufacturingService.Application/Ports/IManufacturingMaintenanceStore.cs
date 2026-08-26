using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingMaintenanceStore
{
    MachineDto CreateMachine(CreateMachineRequest request);
    IReadOnlyList<MachineDto> GetMachines(string? tenantKey, string? status, int limit);
    (MachineTelemetryDto? Telemetry, string? Error, bool Duplicate) RecordMachineTelemetry(Guid machineId, RecordMachineTelemetryRequest request, string tenantKey);
    IReadOnlyList<MachineTelemetryDto> GetMachineTelemetry(Guid machineId, string tenantKey, int limit);
    (MachineDto? Machine, string? Error) RecordMaintenance(Guid machineId, RecordMaintenanceRequest request, string tenantKey);
    (MaintenanceWorkOrderDto? WorkOrder, string? Error) CreateMaintenanceWorkOrder(Guid machineId, CreateMaintenanceWorkOrderRequest request, string tenantKey);
    (MaintenanceWorkOrderDto? WorkOrder, string? Error) CompleteMaintenanceWorkOrder(Guid machineId, Guid workOrderId, CompleteMaintenanceWorkOrderRequest request, string tenantKey);
    IReadOnlyList<MaintenanceWorkOrderDto> GetMaintenanceWorkOrders(string tenantKey, Guid? machineId, string? status, int limit);
    IReadOnlyList<MaintenanceWorkOrderDto> GenerateDueMaintenanceWorkOrders(string tenantKey, DateTimeOffset asOf);
    (DowntimeDto? Downtime, string? Error) CreateDowntime(Guid machineId, CreateDowntimeRequest request, string tenantKey);
    (DowntimeDto? Downtime, string? Error) ResolveDowntime(Guid machineId, Guid downtimeId, ResolveDowntimeRequest request, string tenantKey);
    IReadOnlyList<DowntimeDto> GetDowntimes(string tenantKey, Guid? machineId, string? status, int limit);
}
