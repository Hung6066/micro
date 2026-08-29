using His.Hope.AspNetCore.Authentication;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.ServiceDefaults;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.ManufacturingService.Application;
using His.Hope.ManufacturingService.Infrastructure.Persistence;
using His.Hope.Persistence.Querying;
using His.Hope.Authorization;
using His.Hope.Infrastructure.Security;
using His.Hope.Infrastructure.Caching;
using StackExchange.Redis;
using His.Hope.SharedKernel.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Builder;
using static ManufacturingEndpointHelpers;

internal static class MaintenanceEndpoints
{
    public static RouteGroupBuilder MapMaintenanceEndpoints(this RouteGroupBuilder api)
    {
                
                api.MapGet("/machines", (string? tenantKey, string? status, int? limit, int? page, HttpContext context, IManufacturingMaintenanceStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
                    return Results.Ok(store.GetMachines(scopedTenant, status, limit ?? HisHopePaginationDefaults.DefaultPageSize, page ?? HisHopePaginationDefaults.FirstPage));
                });

                api.MapPost("/machines", (CreateMachineRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
                {
                    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Status))
                        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_machine");
                    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
                    try { return Results.Created("/api/v1/manufacturing/machines", store.CreateMachine(request)); }
                    catch (InvalidOperationException ex) when (ex.Message == "machine_code_exists")
                    { return ManufacturingProblem(StatusCodes.Status409Conflict, ex.Message); }
                });

                api.MapGet("/machines/{machineId:guid}/calibrations", (Guid machineId, int? limit, HttpContext context, IManufacturingMaintenanceStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(store.GetMachineCalibrations(machineId, tenantKey, limit ?? 50));
                });

                api.MapPost("/machines/{machineId:guid}/calibrations", (Guid machineId, CreateMachineCalibrationRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = store.CreateMachineCalibration(machineId, request, tenantKey);
                    return result.Error switch
                    {
                        "tenant_scope_denied" => Results.Forbid(),
                        "invalid_machine_calibration" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        "machine_calibration_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        _ when result.Calibration is not null => Results.Created($"/api/v1/manufacturing/machines/{machineId}/calibrations/{result.Calibration.Id}", result.Calibration),
                        _ => ManufacturingProblem(StatusCodes.Status404NotFound, "machine_not_found")
                    };
                });

                api.MapPost("/machines/{machineId:guid}/maintenance", (Guid machineId, RecordMaintenanceRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = store.RecordMaintenance(machineId, request, tenantKey);
                    return result.Error switch
                    {
                        "tenant_scope_denied" => Results.Forbid(),
                        _ when result.Machine is not null => Results.Ok(result.Machine),
                        _ => ManufacturingProblem(StatusCodes.Status404NotFound, "machine_not_found")
                    };
                });

                api.MapGet("/machines/{machineId:guid}/telemetry", (Guid machineId, int? limit, HttpContext context, IManufacturingMaintenanceStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(store.GetMachineTelemetry(machineId, tenantKey, limit ?? 50));
                });

                api.MapPost("/machines/{machineId:guid}/telemetry", (Guid machineId, RecordMachineTelemetryRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = store.RecordMachineTelemetry(machineId, request, tenantKey);
                    return result.Error switch
                    {
                        "tenant_scope_denied" => Results.Forbid(),
                        "invalid_machine_telemetry" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        _ when result.Telemetry is not null && result.Duplicate => Results.Ok(result.Telemetry),
                        _ when result.Telemetry is not null => Results.Created($"/api/v1/manufacturing/machines/{machineId}/telemetry/{result.Telemetry.EventId}", result.Telemetry),
                        _ => ManufacturingProblem(StatusCodes.Status404NotFound, "machine_not_found")
                    };
                });

                api.MapGet("/maintenance-work-orders", (Guid? machineId, string? status, int? limit, int? page, HttpContext context, IManufacturingMaintenanceStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(store.GetMaintenanceWorkOrders(tenantKey, machineId, status, limit ?? HisHopePaginationDefaults.DefaultPageSize, page ?? HisHopePaginationDefaults.FirstPage));
                });

                api.MapGet("/maintenance-plans", (Guid? machineId, bool? active, int? limit, int? page, HttpContext context, IManufacturingMaintenanceStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(store.GetMaintenancePlans(tenantKey, machineId, active, limit ?? HisHopePaginationDefaults.SmallMaxPageSize, page ?? HisHopePaginationDefaults.FirstPage));
                });

                api.MapPost("/machines/{machineId:guid}/maintenance-plans", (Guid machineId, CreateMaintenancePlanRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = store.CreateMaintenancePlan(machineId, request, tenantKey);
                    return result.Error switch
                    {
                        "machine_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        "tenant_scope_denied" => Results.Forbid(),
                        "invalid_maintenance_plan" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        "maintenance_plan_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        _ => Results.Created($"/api/v1/manufacturing/machines/{machineId}/maintenance-plans/{result.Plan!.Id}", result.Plan)
                    };
                });

                api.MapPost("/maintenance-work-orders/generate", (GenerateMaintenanceWorkOrdersRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var asOf = request.AsOf ?? DateTimeOffset.UtcNow;
                    if (asOf > DateTimeOffset.UtcNow.AddMinutes(5)) return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_generation_time");
                    return Results.Ok(store.GenerateDueMaintenanceWorkOrders(tenantKey, asOf));
                });

                api.MapPost("/machines/{machineId:guid}/maintenance-work-orders", (Guid machineId, CreateMaintenanceWorkOrderRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = store.CreateMaintenanceWorkOrder(machineId, request, tenantKey);
                    return result.Error switch
                    {
                        "tenant_scope_denied" => Results.Forbid(),
                        "invalid_maintenance_work_order" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        "maintenance_work_order_open" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        _ when result.WorkOrder is not null => Results.Created($"/api/v1/manufacturing/maintenance-work-orders/{result.WorkOrder.Id}", result.WorkOrder),
                        _ => ManufacturingProblem(StatusCodes.Status404NotFound, "machine_not_found")
                    };
                });

                api.MapPost("/machines/{machineId:guid}/maintenance-work-orders/{workOrderId:guid}/complete", (Guid machineId, Guid workOrderId, CompleteMaintenanceWorkOrderRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = store.CompleteMaintenanceWorkOrder(machineId, workOrderId, request, tenantKey);
                    return result.Error switch
                    {
                        "tenant_scope_denied" => Results.Forbid(),
                        "invalid_maintenance_completion" or "maintenance_work_order_not_open" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                        "maintenance_work_order_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        _ when result.WorkOrder is not null => Results.Ok(result.WorkOrder),
                        _ => ManufacturingProblem(StatusCodes.Status404NotFound, "machine_not_found")
                    };
                });

                api.MapGet("/machine-downtimes", (Guid? machineId, string? status, int? limit, HttpContext context, IManufacturingMaintenanceStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(store.GetDowntimes(tenantKey, machineId, status, limit ?? 50));
                });

                api.MapPost("/machines/{machineId:guid}/downtimes", (Guid machineId, CreateDowntimeRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = store.CreateDowntime(machineId, request, tenantKey);
                    return result.Error switch
                    {
                        "tenant_scope_denied" => Results.Forbid(),
                        "invalid_downtime" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        "machine_downtime_open" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        _ when result.Downtime is not null => Results.Created($"/api/v1/manufacturing/machines/{machineId}/downtimes/{result.Downtime.Id}", result.Downtime),
                        _ => ManufacturingProblem(StatusCodes.Status404NotFound, "machine_not_found")
                    };
                });

                api.MapPost("/machines/{machineId:guid}/downtimes/{downtimeId:guid}/resolve", (Guid machineId, Guid downtimeId, ResolveDowntimeRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = store.ResolveDowntime(machineId, downtimeId, request, tenantKey);
                    return result.Error switch
                    {
                        "tenant_scope_denied" => Results.Forbid(),
                        "invalid_downtime_end" or "downtime_not_open" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                        "downtime_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        _ when result.Downtime is not null => Results.Ok(result.Downtime),
                        _ => ManufacturingProblem(StatusCodes.Status404NotFound, "machine_not_found")
                    };
                });

                api.MapPatch("/machines/{machineId:guid}", (Guid machineId, UpdateMachineRequest request, HttpContext context, IManufacturingMaintenanceStore store) =>
                {
                    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.UpdateMachine(machineId, request, tenantKey);
                    return result.Error switch { "machine_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "machine_code_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Ok(result.Machine) };
                });

        return api;
    }
}



