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
        api.MapGet("/sop-artifacts", async (string? artifactKey, string? status, int? limit, HttpContext context, IManufacturingComplianceStore store) =>
        {
            var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
            return Results.Ok(await store.GetSopArtifactsAsync(tenantKey, artifactKey, status, limit ?? 100, context.RequestAborted));
        });

        api.MapPost("/sop-artifacts", async (CreateSopArtifactRequest request, HttpContext context, IManufacturingComplianceStore store) =>
        {
            var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
            var actor = context.User.Identity?.Name ?? "operator";
            var result = await store.CreateSopArtifactAsync(request, tenantKey, actor, context.RequestAborted);
            return result.Error switch
            {
                "invalid_sop_artifact" or "invalid_sop_artifact_status" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                "sop_artifact_version_exists" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                _ => Results.Created($"/api/v1/manufacturing/sop-artifacts/{result.Artifact!.Id}", result.Artifact)
            };
        });

        api.MapPost("/sop-artifacts/{artifactId:guid}/approve", async (Guid artifactId, SopArtifactLifecycleRequest request, HttpContext context, IManufacturingComplianceStore store) =>
            await ChangeSopStatusAsync(artifactId, ManufacturingStatusCodes.Approved, request, context, store)).RequireAuthorization(AuthorizationPolicyNames.Permission(HisHopePermissions.Manufacturing.SopApprove));
        api.MapPost("/sop-artifacts/{artifactId:guid}/retire", async (Guid artifactId, SopArtifactLifecycleRequest request, HttpContext context, IManufacturingComplianceStore store) =>
            await ChangeSopStatusAsync(artifactId, "Retired", request, context, store)).RequireAuthorization(AuthorizationPolicyNames.Permission(HisHopePermissions.Manufacturing.SopApprove));
        api.MapPost("/sop-artifacts/{artifactId:guid}/acknowledge", async (Guid artifactId, SopArtifactAcknowledgmentRequest request, HttpContext context, IManufacturingComplianceStore store) =>
        {
            var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
            var actor = context.User.Identity?.Name ?? context.User.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value;
            if (string.IsNullOrWhiteSpace(actor)) return ManufacturingProblem(StatusCodes.Status401Unauthorized, "signature_actor_required");
            var result = await store.AcknowledgeSopArtifactAsync(artifactId, tenantKey, actor, request, context.RequestAborted);
            return result.Error switch
            {
                "sop_artifact_not_found_or_not_approved" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
                "sop_artifact_already_acknowledged" => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                "invalid_sop_artifact_actor" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
                _ => Results.Created($"/api/v1/manufacturing/sop-artifacts/{artifactId}/acknowledgments/{result.Acknowledgment!.Id}", result.Acknowledgment)
            };
        });

        api.MapGet("/business-signatures", async (string? entityType, Guid? entityId, int? limit, HttpContext context, IManufacturingComplianceStore store) =>
        {
            var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
            return Results.Ok(await store.GetBusinessSignaturesAsync(tenantKey, entityType, entityId, limit ?? 100, context.RequestAborted));
        });

        api.MapPost("/business-signatures", async (CreateBusinessSignatureRequest request, HttpContext context, IManufacturingComplianceStore store) =>
        {
            var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
            var actor = context.User.Identity?.Name ?? context.User.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value;
            if (string.IsNullOrWhiteSpace(actor)) return ManufacturingProblem(StatusCodes.Status401Unauthorized, "signature_actor_required");
            var result = await store.CreateBusinessSignatureAsync(tenantKey, actor, request, context.RequestAborted);
            return result.Error switch
            {
                ManufacturingErrorCodes.InvalidBusinessSignature or "invalid_signature_method" => ManufacturingProblem(StatusCodes.Status400BadRequest, result.Error!),
                ManufacturingErrorCodes.BusinessSignatureAlreadyExists => ManufacturingProblem(StatusCodes.Status409Conflict, result.Error!),
                _ => Results.Created($"/api/v1/manufacturing/business-signatures/{result.Signature!.Id}", result.Signature)
            };
        }).RequireAuthorization(AuthorizationPolicyNames.Permission(HisHopePermissions.Manufacturing.BusinessSign));

        return api;
    }

    private static async Task<IResult> ChangeSopStatusAsync(Guid artifactId, string targetStatus, SopArtifactLifecycleRequest request, HttpContext context, IManufacturingComplianceStore store)
    {
        var tenantKey = TenantClaim(context); if (string.IsNullOrWhiteSpace(tenantKey)) return Results.Forbid();
        var result = await store.ChangeSopArtifactStatusAsync(artifactId, tenantKey, targetStatus, request, context.RequestAborted);
        return result.Error switch
        {
            "sop_artifact_not_found" => ManufacturingProblem(StatusCodes.Status404NotFound, result.Error!),
            "invalid_sop_artifact_actor" or "invalid_sop_artifact_transition" => ManufacturingProblem(StatusCodes.Status422UnprocessableEntity, result.Error!),
            _ => Results.Ok(result.Artifact)
        };
    }
}
