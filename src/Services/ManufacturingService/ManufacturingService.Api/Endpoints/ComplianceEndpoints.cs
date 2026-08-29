using His.Hope.Authorization;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.Contracts.Manufacturing;
using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.SharedKernel.Authorization;
using static ManufacturingEndpointHelpers;

internal static class ComplianceEndpoints
{
    public static RouteGroupBuilder MapComplianceEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/sop-artifacts", (string? artifactKey, string? status, int? limit, HttpContext context, IManufacturingComplianceStore store) =>
        {
            var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
            return Results.Ok(store.GetSopArtifacts(tenantKey, artifactKey, status, limit ?? 100));
        });

        api.MapPost("/sop-artifacts", (CreateSopArtifactRequest request, HttpContext context, IManufacturingComplianceStore store) =>
        {
            var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
            var actor = context.User.Identity?.Name ?? "operator";
            var result = store.CreateSopArtifact(request, tenantKey, actor);
            return result.Error switch
            {
                "invalid_sop_artifact" or "invalid_sop_artifact_status" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                "sop_artifact_version_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                _ => Results.Created($"/api/v1/manufacturing/sop-artifacts/{result.Artifact!.Id}", result.Artifact)
            };
        });

        api.MapPost("/sop-artifacts/{artifactId:guid}/approve", (Guid artifactId, SopArtifactLifecycleRequest request, HttpContext context, IManufacturingComplianceStore store) =>
            ChangeSopStatus(artifactId, "Approved", request, context, store)).RequireAuthorization(AuthorizationPolicyNames.Permission(HisHopePermissions.Manufacturing.SopApprove));
        api.MapPost("/sop-artifacts/{artifactId:guid}/retire", (Guid artifactId, SopArtifactLifecycleRequest request, HttpContext context, IManufacturingComplianceStore store) =>
            ChangeSopStatus(artifactId, "Retired", request, context, store)).RequireAuthorization(AuthorizationPolicyNames.Permission(HisHopePermissions.Manufacturing.SopApprove));
        api.MapPost("/sop-artifacts/{artifactId:guid}/acknowledge", (Guid artifactId, SopArtifactAcknowledgmentRequest request, HttpContext context, IManufacturingComplianceStore store) =>
        {
            var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
            var actor = context.User.Identity?.Name ?? context.User.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(actor)) return ManufacturingProblem(StatusCodes.Status401Unauthorized, "signature_actor_required");
            var result = store.AcknowledgeSopArtifact(artifactId, tenantKey, actor, request);
            return result.Error switch
            {
                "sop_artifact_not_found_or_not_approved" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                "sop_artifact_already_acknowledged" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                "invalid_sop_artifact_actor" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                _ => Results.Created($"/api/v1/manufacturing/sop-artifacts/{artifactId}/acknowledgments/{result.Acknowledgment!.Id}", result.Acknowledgment)
            };
        });

        api.MapGet("/business-signatures", (string? entityType, Guid? entityId, int? limit, HttpContext context, IManufacturingComplianceStore store) =>
        {
            var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
            return Results.Ok(store.GetBusinessSignatures(tenantKey, entityType, entityId, limit ?? 100));
        });

        api.MapPost("/business-signatures", (CreateBusinessSignatureRequest request, HttpContext context, IManufacturingComplianceStore store) =>
        {
            var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
            var actor = context.User.Identity?.Name ?? context.User.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(actor)) return ManufacturingProblem(StatusCodes.Status401Unauthorized, "signature_actor_required");
            var result = store.CreateBusinessSignature(tenantKey, actor, request);
            return result.Error switch
            {
                "invalid_business_signature" or "invalid_signature_method" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                "business_signature_already_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                _ => Results.Created($"/api/v1/manufacturing/business-signatures/{result.Signature!.Id}", result.Signature)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Permission(HisHopePermissions.Manufacturing.BusinessSign));

        return api;
    }

    private static IResult ChangeSopStatus(Guid artifactId, string targetStatus, SopArtifactLifecycleRequest request, HttpContext context, IManufacturingComplianceStore store)
    {
        var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
        var result = store.ChangeSopArtifactStatus(artifactId, tenantKey, targetStatus, request);
        return result.Error switch
        {
            "sop_artifact_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
            "invalid_sop_artifact_actor" or "invalid_sop_artifact_transition" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
            _ => Results.Ok(result.Artifact)
        };
    }
}
