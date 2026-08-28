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

internal static class WorkflowEndpoints
{
    public static RouteGroupBuilder MapWorkflowEndpoints(this RouteGroupBuilder api)
    {
                
                api.MapGet("/workflows", () => Results.Ok(ManufacturingWorkflowRegistry.EntityTypes
                    .Select(type => ToWorkflowDto(ManufacturingWorkflowRegistry.TryGet(type)!))
                    .ToList()));

                api.MapGet("/workflows/{entityType}", (string entityType) =>
                {
                    var definition = ManufacturingWorkflowRegistry.TryGet(entityType);
                    return definition is null
                        ? ManufacturingProblem(StatusCodes.Status404NotFound, "workflow_not_found")
                        : Results.Ok(ToWorkflowDto(definition));
                });

                api.MapGet("/entities/{entityType}/{entityId:guid}/cross-workflow", (string entityType, Guid entityId, HttpContext context, IManufacturingWorkflowStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var trace = store.GetCrossEntityWorkflow(tenantKey, entityType, entityId);
                    return trace is null
                        ? ManufacturingProblem(StatusCodes.Status404NotFound, "cross_workflow_not_found")
                        : Results.Ok(trace);
                });

        return api;
    }
}




