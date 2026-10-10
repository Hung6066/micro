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

internal static class QualityEndpoints
{
    public static RouteGroupBuilder MapQualityEndpoints(this RouteGroupBuilder api)
    {
                
                api.MapGet("/lots/{lotId:guid}/quality-inspections", async (Guid lotId, string? tenantKey, int? limit, int? page, HttpContext context, IManufacturingProductionStore store, CancellationToken cancellationToken) =>
                {
                    if (!TryResolveTenant(context, tenantKey, out var scopedTenant)) return Results.Forbid();
                    return Results.Ok(await store.GetQualityInspectionsAsync(lotId, scopedTenant, limit ?? HisHopePaginationDefaults.QualityDefaultPageSize, page ?? HisHopePaginationDefaults.FirstPage, cancellationToken));
                });

                api.MapPost("/quality-inspections", async (CreateQualityInspectionRequest request, HttpContext context, IManufacturingQualityWorkflowStore store) =>
                {
                    if (request.LotId == Guid.Empty || string.IsNullOrWhiteSpace(request.TenantKey) || string.IsNullOrWhiteSpace(request.Inspector))
                        return ManufacturingProblem(StatusCodes.Status400BadRequest, "invalid_quality_inspection");
                    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
                    var result = await store.CreateQualityInspectionAsync(request, context.RequestAborted);
                    return result.Error switch
                    {
                        ManufacturingErrorCodes.LotNotFound or "inspection_plan_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        ManufacturingErrorCodes.TenantMismatch or ManufacturingErrorCodes.InspectionPlanMismatch or "inspection_plan_not_effective" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        "invalid_inspection_status" or "invalid_moisture_percent" or "too_many_quality_test_results" or "invalid_quality_test_result" or "invalid_quality_test_limit" or "quality_test_failure_requires_failed_inspection" or "quality_test_results_do_not_support_pass" or "control_measurement_evidence_required" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        _ => Results.Created("/api/v1/manufacturing/quality-inspections", result.Inspection)
                    };
                });

                api.MapGet("/inspection-plan-versions", (string? productSku, string? status, int? limit, HttpContext context, IManufacturingQualityWorkflowStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(store.GetInspectionPlanVersions(tenantKey, productSku, status, limit ?? 50));
                });

                api.MapGet("/quality-samples", (Guid? inspectionId, string? disposition, int? limit, HttpContext context, IManufacturingQualityWorkflowStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(store.GetQualitySamples(tenantKey, inspectionId, disposition, limit ?? 100));
                });

                api.MapPost("/quality-samples", async (CreateQualitySampleRequest request, HttpContext context, IManufacturingQualityWorkflowStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = await store.CreateQualitySampleAsync(request, tenantKey, context.RequestAborted);
                    return result.Error switch
                    {
                        ManufacturingErrorCodes.QualityInspectionNotFound => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        ManufacturingErrorCodes.TenantScopeDenied => Results.Forbid(),
                        "invalid_quality_sample" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        ManufacturingErrorCodes.QualitySampleExists => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        _ => Results.Created("/api/v1/manufacturing/quality-samples", result.Sample)
                    };
                });

                api.MapPost("/quality-samples/{sampleId:guid}/disposition", async (Guid sampleId, QualitySampleDispositionRequest request, HttpContext context, IManufacturingQualityWorkflowStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = await store.ChangeQualitySampleDispositionAsync(sampleId, tenantKey, request, context.RequestAborted);
                    return result.Error switch
                    {
                        ManufacturingErrorCodes.QualitySampleNotFound => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        ManufacturingErrorCodes.TenantScopeDenied => Results.Forbid(),
                        "invalid_quality_sample_actor" or "invalid_quality_sample_disposition" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                        _ => Results.Ok(result.Sample)
                    };
                });

                api.MapPost("/inspection-plan-versions", async (CreateInspectionPlanVersionRequest request, HttpContext context, IManufacturingQualityWorkflowStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey) || !TenantMatches(context, request.TenantKey)) return Results.Forbid();
                    var result = await store.CreateInspectionPlanVersionAsync(request, context.RequestAborted);
                    return result.Error switch
                    {
                        "invalid_inspection_plan" or "invalid_inspection_plan_status" or "invalid_inspection_plan_dates" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        "inspection_plan_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                        _ => Results.Created("/api/v1/manufacturing/inspection-plan-versions", result.Plan)
                    };
                });

                api.MapPost("/inspection-plan-versions/{planId:guid}/status", async (Guid planId, string status, InspectionPlanLifecycleRequest request, HttpContext context, IManufacturingQualityWorkflowStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = await store.ChangeInspectionPlanLifecycleAsync(planId, tenantKey, status, request, context.RequestAborted);
                    return result.Error switch
                    {
                        "inspection_plan_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        ManufacturingErrorCodes.TenantScopeDenied => Results.Forbid(),
                        "invalid_inspection_plan_actor" or "invalid_inspection_plan_transition" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                        _ => Results.Ok(result.Plan)
                    };
                });

                api.MapGet("/product-specifications", (string? productSku, string? status, int? limit, HttpContext context, IManufacturingQualityWorkflowStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(store.GetProductSpecifications(tenantKey, productSku, status, limit ?? 50));
                });

                api.MapPost("/product-specifications", async (CreateProductSpecificationRequest request, HttpContext context, IManufacturingQualityWorkflowStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    if (!TenantMatches(context, request.TenantKey)) return Results.Forbid();
                    var result = await store.CreateProductSpecificationAsync(request, context.RequestAborted);
                    return result.Error is null
                        ? Results.Created("/api/v1/manufacturing/product-specifications", result.Specification)
                        : ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!);
                });

                api.MapPost("/product-specifications/{specificationId:guid}/approve", async (Guid specificationId, ProductSpecificationLifecycleRequest request, HttpContext context, IManufacturingQualityWorkflowStore store) =>
                    await ChangeProductSpecification(specificationId, ManufacturingStatusCodes.Approved, request, context, store)).RequireAuthorization(AuthorizationPolicyNames.Permission(HisHopePermissions.Manufacturing.SpecificationApprove));

                api.MapPost("/product-specifications/{specificationId:guid}/retire", async (Guid specificationId, ProductSpecificationLifecycleRequest request, HttpContext context, IManufacturingQualityWorkflowStore store) =>
                    await ChangeProductSpecification(specificationId, "Retired", request, context, store));

                
                api.MapGet("/deviations", async (Guid? productionBatchId, string? status, int? limit, HttpContext context, IManufacturingQualityWorkflowStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(await store.GetDeviationsAsync(tenantKey, productionBatchId, status, limit ?? 50, context.RequestAborted));
                });

                api.MapPost("/production-batches/{batchId:guid}/deviations", async (Guid batchId, CreateDeviationRequest request, HttpContext context, IManufacturingQualityWorkflowStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    var result = await store.CreateDeviationAsync(batchId, tenantKey, request, context.RequestAborted);
                    return result.Error switch
                    {
                        "invalid_deviation" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                        ManufacturingErrorCodes.ProductionBatchNotFound => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                        ManufacturingErrorCodes.TenantScopeDenied => Results.Forbid(),
                        "batch_not_active" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                        _ => Results.Created($"/api/v1/manufacturing/deviations/{result.Deviation!.Id}", result.Deviation)
                    };
                });

                api.MapPost("/deviations/{deviationId:guid}/approve", async (Guid deviationId, DeviationActionRequest request, HttpContext context, IManufacturingQualityWorkflowStore store) =>
                    await ChangeDeviationAsync(deviationId, ManufacturingStatusCodes.Approved, request, context, store));

                api.MapPost("/deviations/{deviationId:guid}/reject", async (Guid deviationId, DeviationActionRequest request, HttpContext context, IManufacturingQualityWorkflowStore store) =>
                    await ChangeDeviationAsync(deviationId, ManufacturingStatusCodes.Rejected, request, context, store));

                api.MapPost("/deviations/{deviationId:guid}/close", async (Guid deviationId, DeviationActionRequest request, HttpContext context, IManufacturingQualityWorkflowStore store) =>
                    await ChangeDeviationAsync(deviationId, ManufacturingStatusCodes.Closed, request, context, store));

                api.MapGet("/deviations/{deviationId:guid}/status-history", async (Guid deviationId, HttpContext context, IManufacturingQualityWorkflowStore store) =>
                {
                    var tenantKey = TenantClaim(context);
                    if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
                    return Results.Ok(await store.GetDeviationStatusHistoryAsync(tenantKey, deviationId, context.RequestAborted));
                });

                api.MapGet("/capas", async (string? status, int? limit, HttpContext context, IManufacturingCapaStore store) =>
                {
                    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); return Results.Ok(await store.GetCapasAsync(tenantKey, status, limit ?? 200, context.RequestAborted));
                });

                api.MapPost("/capas", async (CreateCapaRequest request, HttpContext context, IManufacturingCapaStore store) =>
                {
                    var tenantKey = TenantClaim(context); var actor = context.User.Identity?.Name ?? context.User.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value ?? "operator"; if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = await store.CreateCapaAsync(tenantKey, request, actor, context.RequestAborted); return result.Error switch { "invalid_capa" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!), ManufacturingErrorCodes.SupplierNotFound or ManufacturingErrorCodes.DeviationNotFound => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), _ => Results.Created("/api/v1/manufacturing/capas", result.Capa) };
                });

                api.MapPost("/capas/{capaId:guid}/status", async (Guid capaId, UpdateCapaStatusRequest request, HttpContext context, IManufacturingCapaStore store) =>
                {
                    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = await store.UpdateCapaStatusAsync(tenantKey, capaId, request, context.RequestAborted); return result.Error switch { "capa_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), "invalid_capa_actor" or "invalid_capa_transition" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!), _ => Results.Ok(result.Capa) };
                });

                api.MapGet("/supplier-evaluations", async (Guid? supplierId, int? limit, HttpContext context, IManufacturingCapaStore store) =>
                {
                    var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); return Results.Ok(await store.GetSupplierEvaluationsAsync(tenantKey, supplierId, limit ?? 200, context.RequestAborted));
                });

                api.MapPost("/supplier-evaluations", async (CreateSupplierEvaluationRequest request, HttpContext context, IManufacturingCapaStore store) =>
                {
                    var tenantKey = TenantClaim(context); var actor = context.User.Identity?.Name ?? context.User.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value ?? "operator"; if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid(); var result = await store.CreateSupplierEvaluationAsync(tenantKey, request, actor, context.RequestAborted); return result.Error switch { "invalid_supplier_evaluation" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!), ManufacturingErrorCodes.SupplierNotFound => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!), _ => Results.Created("/api/v1/manufacturing/supplier-evaluations", result.Evaluation) };
                });

        return api;
    }
}
