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

internal static class PlanningEndpoints
{
    public static RouteGroupBuilder MapPlanningEndpoints(this RouteGroupBuilder api)
    {
                
                api.MapGet("/sales/forecasts", (string? productSku, int? limit, int? page, HttpContext context, IManufacturingDashboardStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(store.GetSalesForecasts(tenantKey, productSku, limit ?? HisHopePaginationDefaults.DefaultPageSize, page ?? HisHopePaginationDefaults.FirstPage));
                });

                api.MapPost("/sales/forecasts", (CreateSalesForecastRequest request, HttpContext context, IManufacturingDashboardStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    try { return Results.Created("/api/v1/manufacturing/sales/forecasts", store.CreateSalesForecast(tenantKey, request)); }
                    catch (InvalidOperationException ex) when (ex.Message == "invalid_sales_forecast") { return ManufacturingProblem(StatusCodes.Status400BadRequest, ex.Message); }
                    catch (InvalidOperationException ex) when (ex.Message == "forecast_version_exists") { return ManufacturingProblem(StatusCodes.Status409Conflict, ex.Message); }
                });

                api.MapGet("/sales/actuals", (string? productSku, int? limit, HttpContext context, IManufacturingMlDataStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(store.GetSalesActuals(tenantKey, productSku, limit ?? 500));
                });

                api.MapPost("/sales/actuals", (RecordSalesActualRequest request, HttpContext context, IManufacturingMlDataStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var actor = context.User.Identity?.Name ?? request.Actor;
                    var result = store.RecordSalesActual(tenantKey, actor, request);
                    return result.Error is not null
                        ? ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error)
                        : Results.Created("/api/v1/manufacturing/sales/actuals", result.Actual);
                });

                api.MapGet("/ml/datasets/{datasetKey}/snapshots", (string datasetKey, DateTimeOffset? from, DateTimeOffset? to, int? limit, HttpContext context, IManufacturingMlDataStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    if (string.IsNullOrWhiteSpace(datasetKey)) return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_dataset_key");
                    return Results.Ok(store.GetFeatureSnapshots(tenantKey, datasetKey, from, to, limit ?? 5_000));
                });

                api.MapPost("/ml/datasets/{datasetKey}/snapshots", (string datasetKey, MlFeatureSnapshotRequest request, HttpContext context, IManufacturingMlDataStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    if (!string.Equals(datasetKey.Trim(), request.DatasetKey.Trim(), StringComparison.OrdinalIgnoreCase)) return ManufacturingProblem(StatusCodes.Status400BadRequest, ManufacturingErrorCodes.DatasetKeyMismatch);
                    var result = store.CreateFeatureSnapshot(tenantKey, context.User.Identity?.Name ?? "system", request);
                    return result.Error switch
                    {
                        "ml_feature_snapshot_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        "invalid_ml_json" or "invalid_ml_feature_snapshot" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        _ when result.Snapshot is not null => Results.Created($"/api/v1/manufacturing/ml/datasets/{datasetKey}/snapshots/{result.Snapshot.Id}", result.Snapshot),
                        _ => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error ?? "ml_snapshot_failed")
                    };
                });

                api.MapGet("/ml/datasets/{datasetKey}/quality", (string datasetKey, HttpContext context, IManufacturingMlDataStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    if (string.IsNullOrWhiteSpace(datasetKey)) return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_dataset_key");
                    return Results.Ok(store.GetDatasetQuality(tenantKey, datasetKey));
                });

                api.MapGet("/planning/forecast-material-requirements/{forecastId:guid}", (Guid forecastId, HttpContext context, IManufacturingDashboardStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = store.GetSalesForecastMaterialRequirements(tenantKey, forecastId);
                    return result.Error switch
                    {
                        ManufacturingErrorCodes.ForecastNotFound => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        ManufacturingErrorCodes.ApprovedRecipeNotFound => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        _ => Results.Ok(result.Requirements)
                    };
                });

                api.MapGet("/planning/material-requirements", (Guid? productionOrderId, HttpContext context, IManufacturingPlanningWorkflowStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(store.GetMaterialRequirements(tenantKey, productionOrderId));
                });

        return api;
    }
}
