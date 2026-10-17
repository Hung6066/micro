using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingMaintenanceStore
{
    Task<MachineDto> CreateMachineAsync(CreateMachineRequest request, CancellationToken cancellationToken = default);
    Task<(MachineDto? Machine, string? Error)> UpdateMachineAsync(Guid machineId, UpdateMachineRequest request, string tenantKey, CancellationToken cancellationToken = default);
    IReadOnlyList<MachineDto> GetMachines(string? tenantKey, string? status, int limit, int page = 1);
    (MachineCalibrationDto? Calibration, string? Error) CreateMachineCalibration(Guid machineId, CreateMachineCalibrationRequest request, string tenantKey);
    IReadOnlyList<MachineCalibrationDto> GetMachineCalibrations(Guid machineId, string tenantKey, int limit);
    Task<(MachineTelemetryDto? Telemetry, string? Error, bool Duplicate)> RecordMachineTelemetryAsync(Guid machineId, RecordMachineTelemetryRequest request, string tenantKey, CancellationToken cancellationToken = default);
    IReadOnlyList<MachineTelemetryDto> GetMachineTelemetry(Guid machineId, string tenantKey, int limit);
    Task<(MachineDto? Machine, string? Error)> RecordMaintenanceAsync(Guid machineId, RecordMaintenanceRequest request, string tenantKey, CancellationToken cancellationToken = default);
    Task<(MaintenanceWorkOrderDto? WorkOrder, string? Error)> CreateMaintenanceWorkOrderAsync(Guid machineId, CreateMaintenanceWorkOrderRequest request, string tenantKey, CancellationToken cancellationToken = default);
    Task<(MaintenanceWorkOrderDto? WorkOrder, string? Error)> CompleteMaintenanceWorkOrderAsync(Guid machineId, Guid workOrderId, CompleteMaintenanceWorkOrderRequest request, string tenantKey, CancellationToken cancellationToken = default);
    IReadOnlyList<MaintenanceWorkOrderDto> GetMaintenanceWorkOrders(string tenantKey, Guid? machineId, string? status, int limit, int page = 1);
    (MaintenancePlanDto? Plan, string? Error) CreateMaintenancePlan(Guid machineId, CreateMaintenancePlanRequest request, string tenantKey);
    IReadOnlyList<MaintenancePlanDto> GetMaintenancePlans(string tenantKey, Guid? machineId, bool? active, int limit, int page = 1);
    IReadOnlyList<MaintenanceWorkOrderDto> GenerateDueMaintenanceWorkOrders(string tenantKey, DateTimeOffset asOf);
    Task<(DowntimeDto? Downtime, string? Error)> CreateDowntimeAsync(Guid machineId, CreateDowntimeRequest request, string tenantKey, CancellationToken cancellationToken = default);
    Task<(DowntimeDto? Downtime, string? Error)> ResolveDowntimeAsync(Guid machineId, Guid downtimeId, ResolveDowntimeRequest request, string tenantKey, CancellationToken cancellationToken = default);
    IReadOnlyList<DowntimeDto> GetDowntimes(string tenantKey, Guid? machineId, string? status, int limit);
}
