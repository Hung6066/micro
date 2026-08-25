using System.Security.Claims;
using System.Text.Json;
using His.Hope.Authorization;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Authorization;

public static class SupportElevationGuard
{
    public static async Task<IResult?> EnsureCrossTenantMutationAllowedAsync(
        HttpContext http,
        IdentityDbContext db,
        IConglomerateTenantRegistry registry,
        IamTenantScopeFilter filter,
        CancellationToken ct)
    {
        if (!registry.IsEnabled || filter.AccessDenied)
            return null;

        var sourceTenant = http.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrWhiteSpace(sourceTenant))
            return null;

        var targetTenant = filter.AllowedTenantKeys?.Count == 1
            ? filter.AllowedTenantKeys.First()
            : null;
        if (string.IsNullOrWhiteSpace(targetTenant) ||
            string.Equals(sourceTenant, targetTenant, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!registry.IsCustomerTenant(targetTenant))
            return Results.Forbid();

        var memberships = IamTenantScopeResolver.GetMemberships(http.User);
        if (memberships.Any(membership =>
                string.Equals(membership, targetTenant, StringComparison.OrdinalIgnoreCase)))
            return null;

        if (http.RequestServices.GetService(typeof(ICrossTenantAccessPolicy)) is not ConfigurableCrossTenantAccessPolicy policy)
            return Results.Forbid();

        var pair = policy.FindMatchingPair(sourceTenant, targetTenant, "admin.users.write", requiresJit: true);
        if (pair is null)
            return Results.Forbid();

        if (!http.Request.Headers.TryGetValue(ConglomerateConstants.SupportElevationHeader, out var elevationHeader) ||
            !Guid.TryParse(elevationHeader.FirstOrDefault(), out var elevationId))
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Support elevation required",
                detail: "Cross-tenant customer mutations require an approved support elevation.");

        var operatorUserId = ResolveOperatorUserId(http.User);
        var elevation = await db.SupportElevations.AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.Id == elevationId &&
                item.OperatorUserId == operatorUserId &&
                item.Status == "approved" &&
                item.ExpiresAt > DateTime.UtcNow,
                ct);
        if (elevation is null)
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Invalid support elevation",
                detail: "The support elevation is missing, expired, or not approved.");

        if (!string.Equals(elevation.SourceTenant, sourceTenant, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(elevation.TargetTenant, targetTenant, StringComparison.OrdinalIgnoreCase))
            return Results.Forbid();

        SupportElevationHttpContext.SetElevation(http, elevation);
        return null;
    }

    private static Guid ResolveOperatorUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(subject, out var userId) ? userId : Guid.Empty;
    }
}

public static class SupportElevationHttpContext
{
    public const string ItemKey = "__SupportElevation";

    public static void SetElevation(HttpContext http, SupportElevation elevation) =>
        http.Items[ItemKey] = elevation;

    public static SupportElevation? GetElevation(HttpContext http) =>
        http.Items.TryGetValue(ItemKey, out var value) ? value as SupportElevation : null;
}

public static class SupportElevationPermissions
{
    public static bool Allows(SupportElevation elevation, string permissionAction)
    {
        var permissions = JsonSerializer.Deserialize<string[]>(elevation.PermissionsJson) ?? [];
        return permissions.Length == 0 ||
               permissions.Contains(permissionAction, StringComparer.OrdinalIgnoreCase);
    }
}
