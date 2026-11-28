using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.UseCases.Settings.Commands;
using His.Hope.IdentityService.Application.UseCases.Settings.Queries;
using MediatR;
using System.Security.Claims;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Infrastructure.Facility;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// System settings endpoints for hospital-wide configuration.
/// All endpoints require authorization.
/// </summary>
public static class SettingsEndpoints
{
    public static RouteGroupBuilder MapSettingsEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/v1/settings - All settings (key-value)
        group.MapGet(IdentityApiRoutes.SettingsSegment, async (
            IMediator mediator,
            FacilityContext facilityContext,
            CancellationToken ct,
            string? facilityId = null) =>
        {
            var scopeId = ResolveScope(facilityContext, facilityId);
            var settings = await mediator.Send(new GetSettingsQuery(scopeId), ct);
            return Results.Ok(settings);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsRead);

        // GET /api/v1/settings/{key} - Get single setting
        group.MapGet(IdentityApiRoutes.SettingsSegment + "/{key}", async (
            string key,
            IMediator mediator,
            FacilityContext facilityContext,
            CancellationToken ct,
            string? facilityId = null) =>
        {
            var setting = await mediator.Send(new GetSettingByKeyQuery(key, ResolveScope(facilityContext, facilityId)), ct);
            Guard.Against.NotFound(setting, "Setting", key);
            return Results.Ok(setting);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsRead);

        // PUT /api/v1/settings/{key} - Update single setting
        group.MapPut(IdentityApiRoutes.SettingsSegment + "/{key}", async (
            string key,
            UpdateSettingRequest request,
            HttpContext httpContext,
            IMediator mediator,
            His.Hope.Infrastructure.Audit.IAuditService audit,
            FacilityContext facilityContext,
            CancellationToken ct,
            string? facilityId = null) =>
        {
            var changedKeys = new[] { key };
            if (GuardFederationTrustKeys(httpContext, changedKeys) is { } denied)
                return denied;

            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.User.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value;

            var setting = await mediator.Send(
                new UpdateSettingCommand(key, request.Value, request.Description, userId, ResolveScope(facilityContext, facilityId)), ct);
            await AuditSettingsWriteAsync(audit, httpContext, changedKeys, ct);
            return Results.Ok(setting);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsWrite);

        // PUT /api/v1/settings - Bulk update settings
        group.MapPut(IdentityApiRoutes.SettingsSegment, async (
            List<BulkUpdateSettingItem> request,
            HttpContext httpContext,
            IMediator mediator,
            His.Hope.Infrastructure.Audit.IAuditService audit,
            FacilityContext facilityContext,
            CancellationToken ct,
            string? facilityId = null) =>
        {
            var changedKeys = request.Select(item => item.Key).ToArray();
            if (GuardFederationTrustKeys(httpContext, changedKeys) is { } denied)
                return denied;

            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.User.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value;

            var settings = await mediator.Send(
                new BulkUpdateSettingsCommand(request, userId, ResolveScope(facilityContext, facilityId)), ct);
            await AuditSettingsWriteAsync(audit, httpContext, changedKeys, ct);
            return Results.Ok(settings);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsWrite);

        // Frontend admin module calls PUT /api/v1/admin/settings/bulk.
        group.MapPut(IdentityApiRoutes.SettingsSegment + "/bulk", async (
            BulkUpdateSettingsRequest request,
            HttpContext httpContext,
            IMediator mediator,
            His.Hope.Infrastructure.Audit.IAuditService audit,
            FacilityContext facilityContext,
            CancellationToken ct,
            string? facilityId = null) =>
        {
            var changedKeys = request.Settings.Select(item => item.Key).ToArray();
            if (GuardFederationTrustKeys(httpContext, changedKeys) is { } denied)
                return denied;

            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.User.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value;

            var settings = await mediator.Send(
                new BulkUpdateSettingsCommand(request.Settings, userId, ResolveScope(facilityContext, facilityId)), ct);
            await AuditSettingsWriteAsync(audit, httpContext, changedKeys, ct);
            return Results.Ok(settings);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsWrite);

        return group;
    }

    /// <summary>
    /// Saml2:* and Ldap:* decide which identity provider this service trusts and
    /// how its groups map to roles. Changing them can sign an attacker in as any
    /// federated user or point the directory bind at another host, so they are
    /// reserved for a super admin with a fresh MFA step-up.
    /// </summary>
    private static IResult? GuardFederationTrustKeys(HttpContext http, IReadOnlyCollection<string> keys)
    {
        var touchesFederationTrust = keys.Any(key =>
            key.StartsWith("Saml2:", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("Ldap:", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("Authentication:", StringComparison.OrdinalIgnoreCase));
        if (!touchesFederationTrust)
            return null;

        var isSuperAdmin = string.Equals(
            http.User.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.SuperAdmin)?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase);
        if (!isSuperAdmin)
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Federation settings are restricted",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "federation_settings_restricted" });

        return His.Hope.IdentityService.Api.Authorization.StepUpAuthenticationGuard.RequireFreshMfa(http);
    }

    private static async Task AuditSettingsWriteAsync(
        His.Hope.Infrastructure.Audit.IAuditService audit,
        HttpContext http,
        IReadOnlyCollection<string> keys,
        CancellationToken ct)
    {
        foreach (var key in keys)
            await AdminAudit.LogAsync(audit, http, "UPDATE", "Setting", key, ct);
    }

    private static string? ResolveScope(FacilityContext context, string? requestedFacility)
    {
        if (!context.IsCrossFacility && !string.IsNullOrWhiteSpace(context.FacilityId))
            return context.FacilityId;
        return string.IsNullOrWhiteSpace(requestedFacility) ? IdentityScope.Global : requestedFacility.Trim();
    }
}

public sealed record BulkUpdateSettingsRequest(List<BulkUpdateSettingItem> Settings);
