using System.Security.Claims;
using His.Hope.Authorization;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Authorization;

public static class IamTenantAccessGuard
{
    public static IResult? ForbidIfDenied(IamTenantScopeFilter filter) =>
        filter.AccessDenied ? Results.Forbid() : null;

    public static async Task<IResult?> EnsureUserAccessAsync(
        IdentityDbContext db,
        Guid userId,
        IamTenantScopeFilter filter,
        CancellationToken ct)
    {
        if (ForbidIfDenied(filter) is { } denied)
            return denied;

        if (filter.AllowedTenantKeys is null)
            return null;

        if (!await IamTenantQueryExtensions.UserHasTenantAccessAsync(
                db, userId, filter.AllowedTenantKeys, ct))
            return Results.NotFound();

        return null;
    }

    public static IResult? EnsureScopeAccess(Guid scopeId, IamTenantScopeFilter filter)
    {
        if (ForbidIfDenied(filter) is { } denied)
            return denied;

        if (filter.AllowedScopeIds is null)
            return null;

        return filter.AllowedScopeIds.Contains(scopeId)
            ? null
            : Results.Forbid();
    }

    public static IResult? EnsureClientAccess(
        string? clientId,
        IConglomerateTenantRegistry tenantRegistry,
        IamTenantScopeFilter filter)
    {
        if (ForbidIfDenied(filter) is { } denied)
            return denied;

        var allowedClientIds = IamTenantQueryExtensions.ResolveAllowedClientIds(tenantRegistry, filter);
        if (allowedClientIds is null)
            return null;

        return allowedClientIds.Contains(clientId ?? string.Empty)
            ? null
            : Results.NotFound();
    }

    public static async Task<IResult?> EnsureRoleVisibleAsync(
        IdentityDbContext db,
        Guid roleId,
        IamTenantScopeFilter filter,
        CancellationToken ct)
    {
        if (ForbidIfDenied(filter) is { } denied)
            return denied;

        if (filter.AllowedTenantKeys is null)
            return null;

        if (filter.AllowedTenantKeys.Count == 0)
            return Results.NotFound();

        var visible = await db.Roles.AsNoTracking()
            .Where(role => role.Id == roleId)
            .Where(role =>
                db.Set<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>()
                    .Any(userRole =>
                        userRole.RoleId == role.Id &&
                        db.UserClaims.Any(claim =>
                            claim.UserId == userRole.UserId &&
                            claim.ClaimType == IamTenantScopeResolver.TenantMembershipClaimType &&
                            filter.AllowedTenantKeys.Contains(claim.ClaimValue))) ||
                db.AccessRequests.Any(request =>
                    request.RoleIdsJson.Contains(role.Id.ToString()) &&
                    db.UserClaims.Any(claim =>
                        claim.UserId == request.SubjectUserId &&
                        claim.ClaimType == IamTenantScopeResolver.TenantMembershipClaimType &&
                        filter.AllowedTenantKeys.Contains(claim.ClaimValue))) ||
                db.AccessReviews.Any(review =>
                    review.RoleIdsJson.Contains(role.Id.ToString()) &&
                    db.UserClaims.Any(claim =>
                        claim.UserId == review.SubjectUserId &&
                        claim.ClaimType == IamTenantScopeResolver.TenantMembershipClaimType &&
                        filter.AllowedTenantKeys.Contains(claim.ClaimValue))))
            .AnyAsync(ct);

        return visible ? null : Results.NotFound();
    }

    public static IResult? EnsureCrossTenantRead(
        ClaimsPrincipal user,
        IamTenantScopeFilter filter,
        string permissionAction,
        ICrossTenantAccessPolicy crossTenantPolicy)
    {
        if (filter.AllowedTenantKeys is null || !filter.IsGroupHqOperator)
            return null;

        var memberships = IamTenantScopeResolver.GetMemberships(user);
        foreach (var targetTenant in filter.AllowedTenantKeys)
        {
            if (string.Equals(
                    targetTenant,
                    IamTenantScopeResolver.GroupHqTenantKey,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            if (memberships.Any(membership =>
                    string.Equals(membership, targetTenant, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (!crossTenantPolicy.IsCrossTenantAllowed(
                    IamTenantScopeResolver.GroupHqTenantKey,
                    targetTenant,
                    permissionAction))
                return Results.Forbid();
        }

        return null;
    }

    public static async Task<(IamTenantScopeFilter Filter, IResult? Error)> ResolveForReadAsync(
        IdentityDbContext db,
        ClaimsPrincipal user,
        Guid? scopeId,
        string permissionAction,
        ICrossTenantAccessPolicy crossTenantPolicy,
        CancellationToken ct) =>
        await ResolveForReadAsync(db, user, scopeId, permissionAction, crossTenantPolicy, registry: null, ct);

    public static async Task<(IamTenantScopeFilter Filter, IResult? Error)> ResolveForReadAsync(
        IdentityDbContext db,
        ClaimsPrincipal user,
        Guid? scopeId,
        string permissionAction,
        ICrossTenantAccessPolicy crossTenantPolicy,
        IConglomerateTenantRegistry? registry,
        CancellationToken ct)
    {
        var filter = await IamTenantScopeResolver.ResolveAsync(db, user, scopeId, registry, ct);
        if (filter.AccessDenied)
            return (filter, Results.Forbid());

        var crossTenantError = EnsureCrossTenantRead(user, filter, permissionAction, crossTenantPolicy);
        if (crossTenantError is not null)
            return (filter, crossTenantError);

        if (registry?.IsEnabled == true &&
            filter.AllowedTenantKeys is { Count: > 0 } targetTenants &&
            crossTenantPolicy is ConfigurableCrossTenantAccessPolicy configurablePolicy)
        {
            var sourceTenant = user.FindFirst("tenant_id")?.Value;
            if (!string.IsNullOrWhiteSpace(sourceTenant))
            {
                foreach (var targetTenant in targetTenants)
                {
                    if (string.Equals(sourceTenant, targetTenant, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (IamTenantScopeResolver.GetMemberships(user).Any(membership =>
                            string.Equals(membership, targetTenant, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    if (configurablePolicy.FindMatchingPair(
                            sourceTenant,
                            targetTenant,
                            permissionAction,
                            requiresJit: false) is null)
                        return (filter, Results.Forbid());
                }
            }
        }

        return (filter, null);
    }

    public static async Task<(IamTenantScopeFilter Filter, IResult? Error)> ResolveForMutationAsync(
        IdentityDbContext db,
        ClaimsPrincipal user,
        Guid? scopeId,
        CancellationToken ct) =>
        await ResolveForMutationAsync(db, user, scopeId, registry: null, http: null, ct);

    public static async Task<(IamTenantScopeFilter Filter, IResult? Error)> ResolveForMutationAsync(
        IdentityDbContext db,
        ClaimsPrincipal user,
        Guid? scopeId,
        IConglomerateTenantRegistry? registry,
        HttpContext? http,
        CancellationToken ct)
    {
        var filter = await IamTenantScopeResolver.ResolveAsync(db, user, scopeId, registry, ct);
        if (ForbidIfDenied(filter) is { } denied)
            return (filter, denied);

        if (registry?.IsEnabled == true && http is not null)
        {
            var elevationError = await SupportElevationGuard.EnsureCrossTenantMutationAllowedAsync(
                http, db, registry, filter, ct);
            if (elevationError is not null)
                return (filter, elevationError);
        }

        return (filter, null);
    }
}
