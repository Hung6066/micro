using His.Hope.AspNetCore.Authentication;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.ServiceDefaults;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.ManufacturingService.Application;
using His.Hope.ManufacturingService.Infrastructure.Persistence;
using His.Hope.Authorization;
using His.Hope.Infrastructure.Security;
using His.Hope.Infrastructure.Caching;
using StackExchange.Redis;
using His.Hope.SharedKernel.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Builder;
using static ManufacturingEndpointHelpers;

internal static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder api)
    {
                
                api.MapGet("/dashboard/manufacturing-summary", (string? tenantKey, HttpContext context, IManufacturingDashboardStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
                    return Results.Ok(store.GetDashboardSummary(scopedTenant));
                });

                api.MapGet("/dashboard/production-kpis", (string? tenantKey, HttpContext context, IManufacturingDashboardStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
                    return Results.Ok(store.GetProductionKpis(scopedTenant));
                });

                api.MapGet("/dashboard/machine-health", (string? tenantKey, int? dueWithinDays, HttpContext context, IManufacturingDashboardStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
                    return Results.Ok(store.GetMachineHealth(scopedTenant, dueWithinDays ?? 7));
                });

                api.MapGet("/dashboard/oee", (Guid? machineId, string? tenantKey, HttpContext context, IManufacturingDashboardStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
                    return Results.Ok(store.GetOee(scopedTenant, machineId));
                });

                api.MapGet("/dashboard/production-costs", (string? tenantKey, HttpContext context, IManufacturingDashboardStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
                    return Results.Ok(store.GetProductionCosts(scopedTenant));
                });

                api.MapGet("/dashboard/exceptions", (string? tenantKey, int? expiryWithinDays, int? downtimeThresholdHours, HttpContext context, IManufacturingDashboardStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
                    return Results.Ok(store.GetExecutiveExceptions(scopedTenant, expiryWithinDays ?? 7, downtimeThresholdHours ?? 4));
                });

                api.MapGet("/dashboard/cost-projection", (string productSku, int? recipeVersion, decimal? plannedQuantity, HttpContext context, IManufacturingDashboardStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    if (string.IsNullOrWhiteSpace(productSku) || !plannedQuantity.HasValue || plannedQuantity <= 0)
                        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_cost_projection");
                
                    var projection = store.GetCostProjection(tenantKey, productSku, recipeVersion, plannedQuantity.Value);
                    return projection is null
                        ? ManufacturingProblem(StatusCodes.Status404NotFound, "recipe_not_found")
                        : Results.Ok(projection);
                });

        return api;
    }
}





