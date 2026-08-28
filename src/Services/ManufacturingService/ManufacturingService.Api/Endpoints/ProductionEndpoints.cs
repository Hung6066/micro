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

internal static class ProductionEndpoints
{
    public static RouteGroupBuilder MapProductionEndpoints(this RouteGroupBuilder api)
    {
                
                api.MapGet("/transformations", (string? tenantKey, string? processStep, int? limit, HttpContext context, IManufacturingProductionStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
                    return Results.Ok(store.GetTransformationSummaries(scopedTenant, processStep, limit ?? 50));
                });

                api.MapGet("/recipes", (string? tenantKey, string? productSku, bool? active, int? limit, HttpContext context, IManufacturingRecipeWorkflowStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
                    return Results.Ok(store.GetRecipes(scopedTenant, productSku, active, limit ?? 50));
                });

                api.MapPost("/recipes", (CreateRecipeRequest request, HttpContext context, IManufacturingRecipeWorkflowStore store) =>
                {
                    if (string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.ProductSku) ||
                        request.Version <= 0 || string.IsNullOrWhiteSpace(request.ProcessStep) || string.IsNullOrWhiteSpace(request.OutputUom) ||
                        request.Components is null || request.Components.Count == 0 || request.Components.Any(x => string.IsNullOrWhiteSpace(x.IngredientSku) || string.IsNullOrWhiteSpace(x.Uom) || x.Quantity <= 0) ||
                        request.TargetYieldPercent <= 0 || request.TargetYieldPercent > 100)
                        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_recipe");
                    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
                    try { return Results.Created("/api/v1/manufacturing/recipes", store.CreateRecipe(request)); }
                    catch (InvalidOperationException ex) when (ex.Message == "recipe_version_exists")
                    { return ManufacturingProblem(StatusCodes.Status409Conflict, ex.Message); }
                    catch (InvalidOperationException ex) when (ex.Message == "invalid_recipe_status")
                    { return ManufacturingProblem(StatusCodes.Status400BadRequest, ex.Message); }
                    catch (InvalidOperationException ex) when (ex.Message == "invalid_product_specification")
                    { return ManufacturingProblem(StatusCodes.Status400BadRequest, ex.Message); }
                });

                api.MapPost("/recipes/{recipeId:guid}/submit", (Guid recipeId, RecipeLifecycleRequest request, HttpContext context, IManufacturingRecipeWorkflowStore store) =>
                    ChangeRecipeLifecycle(recipeId, "Submitted", request, context, store));

                api.MapPost("/recipes/{recipeId:guid}/approve", (Guid recipeId, RecipeLifecycleRequest request, HttpContext context, IManufacturingRecipeWorkflowStore store) =>
                    ChangeRecipeLifecycle(recipeId, "Approved", request, context, store)).RequireAuthorization(AuthorizationPolicyNames.Permission(HisHopePermissions.Manufacturing.RecipeApprove));

                api.MapPost("/recipes/{recipeId:guid}/retire", (Guid recipeId, RecipeLifecycleRequest request, HttpContext context, IManufacturingRecipeWorkflowStore store) =>
                    ChangeRecipeLifecycle(recipeId, "Retired", request, context, store));

                
                api.MapPost("/transformations", (CreateTransformationRequest request, HttpContext context, IManufacturingProductionStore store) =>
                {
                    if (request.Inputs is null || request.Inputs.Count == 0 || request.OutputQuantity <= 0 ||
                        string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.OutputSku) ||
                        string.IsNullOrWhiteSpace(request.OutputUom))
                        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_transformation");
                    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
                
                    var result = store.CreateTransformation(request);
                    return result.Error is not null
                        ? (result.Error is "duplicate_input_lot" or "input_lot_not_released" or "input_quantity_exceeds_available" or "reservation_not_found" or "reservation_mismatch" or "reservation_unavailable"
                            ? ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!)
                            : ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!))
                        : Results.Created($"/api/v1/manufacturing/transformations/{result.Transformation!.Id}", result.Transformation);
                });

                api.MapGet("/production-batches/{batchId:guid}/measurements", (Guid batchId, int? limit, HttpContext context, IManufacturingMlDataStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(store.GetOperationMeasurements(tenantKey, batchId, limit ?? 500));
                });

                api.MapPost("/production-batches/{batchId:guid}/measurements", (Guid batchId, RecordOperationMeasurementRequest request, HttpContext context, IManufacturingMlDataStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    if (request.ProductionBatchId != batchId) return ManufacturingProblem(StatusCodes.Status400BadRequest, "production_batch_mismatch");
                    var actor = context.User.Identity?.Name ?? "operator";
                    var result = store.RecordOperationMeasurement(tenantKey, actor, request);
                    return result.Error switch
                    {
                        "production_batch_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        "invalid_operation_measurement" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        _ when result.Measurement is not null => Results.Created($"/api/v1/manufacturing/production-batches/{batchId}/measurements/{result.Measurement.Id}", result.Measurement),
                        _ => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error ?? "operation_measurement_failed")
                    };
                });

                api.MapGet("/production-batches/{batchId:guid}/cost", (Guid batchId, HttpContext context, IManufacturingDashboardStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var cost = store.GetBatchCost(batchId, tenantKey);
                    return Results.Ok(cost);
                });

                api.MapPost("/production-batches/{batchId:guid}/cost", (Guid batchId, CalculateBatchCostRequest request, HttpContext context, IManufacturingDashboardStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = store.CalculateBatchCost(batchId, tenantKey, request);
                    return result.Error switch
                    {
                        "invalid_batch_cost" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        "production_batch_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        "tenant_scope_denied" => Results.Forbid(),
                        _ => Results.Ok(result.Cost)
                    };
                }).RequireAuthorization(AuthorizationPolicyNames.Permission(HisHopePermissions.Manufacturing.CostManage));

                api.MapGet("/production-orders", (string? tenantKey, string? status, int? limit, HttpContext context, IManufacturingProductionOrderStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
                    return Results.Ok(store.GetOrders(scopedTenant, status, limit ?? 100));
                });

                api.MapPost("/production-orders", (CreateProductionOrderRequest request, HttpContext context, IManufacturingProductionOrderStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    if (string.IsNullOrWhiteSpace(request.OrderNumber) || string.IsNullOrWhiteSpace(request.ProductSku) || string.IsNullOrWhiteSpace(request.OutputUom) || request.RecipeId == Guid.Empty || request.TargetQuantity <= 0)
                        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_production_order");
                    var result = store.CreateOrder(tenantKey, request);
                    return result.Error switch
                    {
                        "invalid_production_order" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        "production_order_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        "recipe_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        "recipe_unavailable" or "recipe_product_mismatch" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                        _ => Results.Created("/api/v1/manufacturing/production-orders", result.Order)
                    };
                });

                api.MapPost("/production-orders/{orderId:guid}/release", (Guid orderId, HttpContext context, IManufacturingProductionOrderStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = store.ReleaseOrder(tenantKey, orderId);
                    return result.Error switch
                    {
                        "production_order_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        "tenant_mismatch" => Results.Forbid(),
                        _ => Results.Ok(result.Order)
                    };
                });

                api.MapPost("/production-orders/{orderId:guid}/cancel", (Guid orderId, HttpContext context, IManufacturingProductionOrderStore store) =>
                {
                    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.CancelOrder(tenantKey, orderId);
                    return result.Error switch { "production_order_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "production_order_not_cancellable" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Ok(result.Order) };
                });

                api.MapGet("/production-batches", (string? tenantKey, string? status, int? limit, HttpContext context, IManufacturingProductionOrderStore store) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
                    return Results.Ok(store.GetBatches(scopedTenant, status, limit ?? 100));
                });

                api.MapPost("/production-batches", (CreateProductionBatchRequest request, HttpContext context, IManufacturingProductionOrderStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    if (request.ProductionOrderId == Guid.Empty || string.IsNullOrWhiteSpace(request.BatchNumber) || request.PlannedQuantity is <= 0 ||
                        request.Inputs?.Any(x => x.LotId == Guid.Empty || x.ReservationId == Guid.Empty || x.Quantity <= 0) == true)
                        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_production_batch");
                    var result = store.CreateBatch(tenantKey, request);
                    return result.Error switch
                    {
                        "production_order_not_found" or "machine_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        "tenant_mismatch" => Results.Forbid(),
                        "invalid_production_batch" or "production_order_not_released" or "machine_unavailable" or "input_lot_not_released" or "input_reservation_unavailable" or "input_reservation_mismatch" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                        "production_batch_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        _ => Results.Created("/api/v1/manufacturing/production-batches", result.Batch)
                    };
                });

                api.MapPost("/production-batches/{batchId:guid}/start", (Guid batchId, HttpContext context, IManufacturingProductionOrderStore store) => BatchStatusResult(batchId, "Started", context, store))
                    .RequireAuthorization(AuthorizationPolicyNames.Permission(HisHopePermissions.Manufacturing.ProductionExecute));

                api.MapPost("/production-batches/{batchId:guid}/pause", (Guid batchId, HttpContext context, IManufacturingProductionOrderStore store) => BatchStatusResult(batchId, "Paused", context, store));

                api.MapPost("/production-batches/{batchId:guid}/resume", (Guid batchId, HttpContext context, IManufacturingProductionOrderStore store) => BatchStatusResult(batchId, "Started", context, store));

                api.MapPost("/production-batches/{batchId:guid}/complete", (Guid batchId, HttpContext context, IManufacturingProductionOrderStore store) => BatchStatusResult(batchId, "Completed", context, store));

                api.MapPost("/production-batches/{batchId:guid}/cancel", (Guid batchId, HttpContext context, IManufacturingProductionOrderStore store) =>
                {
                    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = store.CancelBatch(tenantKey, batchId);
                    return result.Error switch { "production_batch_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "production_batch_not_cancellable" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!), _ => Results.Ok(result.Batch) };
                });

                api.MapGet("/production-batches/{batchId:guid}/status-history", (Guid batchId, HttpContext context, IManufacturingProductionOrderStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(store.GetBatchStatusHistory(tenantKey, batchId));
                });

                api.MapPost("/production-batches/{batchId:guid}/operations", (Guid batchId, RecordOperationRequest request, HttpContext context, IManufacturingProductionOrderStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    if (request.Sequence < 0 || string.IsNullOrWhiteSpace(request.ProcessStep) || string.IsNullOrWhiteSpace(request.Operator) || request.InputQuantity <= 0 || request.OutputQuantity < 0)
                        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_operation_measurement");
                    var result = store.RecordOperation(tenantKey, batchId, request);
                    return result.Error switch
                    {
                        "production_batch_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        "tenant_mismatch" => Results.Forbid(),
                        "batch_not_started" or "invalid_operation_measurement" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                        "operation_sequence_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        _ => Results.Created($"/api/v1/manufacturing/production-batches/{batchId}/operations", result.Operation)
                    };
                });

                api.MapPost("/production-batches/{batchId:guid}/operations/{operationId:guid}/loss-review", (Guid batchId, Guid operationId, LossReviewRequest request, HttpContext context, IManufacturingRecipeWorkflowStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = store.ReviewLoss(tenantKey, batchId, operationId, request);
                    return result.Error switch
                    {
                        "invalid_loss_review" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        "production_batch_not_found" or "operation_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        _ => Results.Ok(result.Review)
                    };
                });

        return api;
    }
}



