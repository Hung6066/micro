using His.Hope.AspNetCore.Authentication;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.ServiceDefaults;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.ManufacturingService.Application;
using His.Hope.ManufacturingService.Domain;
using His.Hope.ManufacturingService.Infrastructure.Persistence;
using His.Hope.Authorization;
using His.Hope.Infrastructure.Security;
using His.Hope.Infrastructure.Caching;
using StackExchange.Redis;
using His.Hope.SharedKernel.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Builder;

internal static class ManufacturingEndpointHelpers
{
    internal static string? TenantClaim(HttpContext context) =>
        context.RequestServices.GetService<IHisHopeTenantContext>()?.TenantKey
        ?? ManufacturingHttpExtensions.ResolveActiveTenant(context);

    internal static bool TenantMatches(HttpContext context, string tenantKey) =>
        ManufacturingHttpExtensions.TenantMatches(context, tenantKey);

    internal static bool TryResolveTenant(HttpContext context, string? requestedTenant, out string tenantKey) =>
        ManufacturingHttpExtensions.TryResolveTenant(context, requestedTenant, out tenantKey);

    internal static IResult ManufacturingProblem(int statusCode, string errorCode) =>
        Results.Problem(
            statusCode: statusCode,
            extensions: new Dictionary<string, object?> { ["errorCode"] = errorCode });

    internal static IResult ChangeRecipeLifecycle(Guid recipeId, string status, RecipeLifecycleRequest request, HttpContext context, IManufacturingRecipeWorkflowStore store)
            {
                var tenantKey = TenantClaim(context);
                if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                var result = store.ChangeRecipeLifecycle(recipeId, tenantKey, status, request);
                return result.Error switch
                {
                    ManufacturingErrorCodes.TenantScopeDenied => Results.Forbid(),
                    ManufacturingErrorCodes.RecipeNotFound => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                    "invalid_recipe_actor" or "invalid_recipe_transition" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                    _ when result.Recipe is not null => Results.Ok(result.Recipe),
                    _ => ManufacturingProblem(StatusCodes.Status404NotFound, ManufacturingErrorCodes.RecipeNotFound)
                };
            }

    internal static IResult ChangeProductSpecification(Guid specificationId, string targetStatus, ProductSpecificationLifecycleRequest request, HttpContext context, IManufacturingQualityWorkflowStore store)
            {
                var tenantKey = TenantClaim(context);
                if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                var result = store.ChangeProductSpecificationLifecycle(specificationId, tenantKey, targetStatus, request);
                return result.Error switch
                {
                    ManufacturingErrorCodes.ProductSpecificationNotFound => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                    ManufacturingErrorCodes.TenantScopeDenied => Results.Forbid(),
                    ManufacturingErrorCodes.ActiveProductSpecificationExists => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                    "invalid_product_specification_actor" or "invalid_product_specification_transition" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                    _ when result.Specification is not null => Results.Ok(result.Specification),
                    _ => ManufacturingProblem(StatusCodes.Status404NotFound, ManufacturingErrorCodes.ProductSpecificationNotFound)
                };
            }

    internal static IResult ChangeDeviation(Guid deviationId, string targetStatus, DeviationActionRequest request, HttpContext context, IManufacturingQualityWorkflowStore store)
            {
                var tenantKey = TenantClaim(context);
                if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                var result = store.ChangeDeviationStatus(deviationId, tenantKey, targetStatus, request);
                return result.Error switch
                {
                    ManufacturingErrorCodes.DeviationNotFound => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                    ManufacturingErrorCodes.TenantScopeDenied => Results.Forbid(),
                    "invalid_deviation_actor" or "invalid_deviation_transition" or "author_cannot_approve_own_deviation" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                    _ when result.Deviation is not null => Results.Ok(result.Deviation),
                    _ => ManufacturingProblem(StatusCodes.Status404NotFound, ManufacturingErrorCodes.DeviationNotFound)
                };
            }

    internal static ManufacturingWorkflowDefinitionDto ToWorkflowDto(ManufacturingWorkflowDefinition definition) =>
            new(
                definition.EntityType,
                definition.Steps.Select(step => new WorkflowStepDefinitionDto(step.Key, step.I18nGroup)).ToList(),
                definition.StatusAliases,
                definition.TerminalStatuses);

    internal static IResult BatchStatusResult(Guid batchId, string targetStatus, HttpContext context, IManufacturingProductionOrderStore store)
        {
            var tenantKey = TenantClaim(context);
            if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
            var result = store.ChangeBatchStatus(tenantKey, batchId, targetStatus);
            return result.Error switch
            {
                ManufacturingErrorCodes.ProductionBatchNotFound => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
            ManufacturingErrorCodes.TenantMismatch => Results.Forbid(),
                ManufacturingErrorCodes.RequiredOperationIncomplete or ManufacturingErrorCodes.QualityGateIncomplete or ManufacturingErrorCodes.MachineUnavailable or ManufacturingErrorCodes.InvalidBatchTransition or ManufacturingErrorCodes.InputReservationUnavailable or ManufacturingErrorCodes.InputQuantityInsufficient or "loss_review_required" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                _ => Results.Ok(result.Batch)
            };
        }
}
