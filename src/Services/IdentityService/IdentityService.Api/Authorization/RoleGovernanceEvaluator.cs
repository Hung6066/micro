using System.Security.Claims;
using His.Hope.IdentityService.Application.Authorization;
using His.Hope.IdentityService.Application.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Authorization;

/// <summary>
/// Server-side guard for delegated role administration. The admin UI may
/// request a grant, but this evaluator limits it to the actor's effective
/// permissions and facility scope.
/// </summary>
public static class RoleGovernanceEvaluator
{
    public static async Task<string?> ValidateRolePermissionsAsync(
        IApplicationDbContext db,
        ClaimsPrincipal actor,
        IEnumerable<string>? requestedPermissions,
        CancellationToken ct)
    {
        var requested = RoleGovernanceRules.NormalizePermissionCodes(requestedPermissions);
        var actorPermissions = await GetActorPermissionsAsync(db, actor, ct);
        var unrestricted = actorPermissions.Contains("admin.permissions.write", StringComparer.OrdinalIgnoreCase);
        var outsideScope = RoleGovernanceRules.FindPermissionOutsideScope(requested, actorPermissions, unrestricted);
        return outsideScope is null
            ? null
            : $"ROLE_GRANT_OUT_OF_SCOPE: actor cannot grant permission '{outsideScope}'.";
    }

    public static async Task<string?> ValidateRoleAssignmentAsync(
        IApplicationDbContext db,
        ClaimsPrincipal actor,
        Guid targetUserId,
        IEnumerable<string>? roleIds,
        CancellationToken ct)
    {
        var roleNames = (roleIds ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var requestedIds = roleNames
            .Where(value => Guid.TryParse(value, out _))
            .Select(Guid.Parse)
            .ToArray();
        var requestedNames = roleNames
            .Where(value => !Guid.TryParse(value, out _))
            .Select(value => value.ToUpperInvariant())
            .ToArray();
        var roleCandidates = await db.Roles.AsNoTracking()
            .Where(candidate => requestedIds.Contains(candidate.Id) ||
                (candidate.NormalizedName != null && requestedNames.Contains(candidate.NormalizedName)))
            .Select(candidate => new { candidate.Id, candidate.Name, candidate.NormalizedName })
            .ToListAsync(ct);

        var roles = new List<(Guid Id, string Name)>();
        foreach (var value in roleNames)
        {
            var role = Guid.TryParse(value, out var id)
                ? roleCandidates.FirstOrDefault(candidate => candidate.Id == id)
                : roleCandidates.FirstOrDefault(candidate =>
                    string.Equals(candidate.NormalizedName, value, StringComparison.OrdinalIgnoreCase));
            if (role is null) return $"ROLE_NOT_FOUND: role '{value}' was not found.";
            roles.Add((role.Id, role.Name ?? string.Empty));
        }

        var requestedPermissions = await db.RolePermissions.AsNoTracking()
            .Where(link => roles.Select(role => role.Id).Contains(link.RoleId))
            .Select(link => link.PermissionCode)
            .Distinct()
            .ToArrayAsync(ct);
        var actorPermissions = await GetActorPermissionsAsync(db, actor, ct);
        var unrestricted = actorPermissions.Contains("admin.permissions.write", StringComparer.OrdinalIgnoreCase);
        var outsideScope = RoleGovernanceRules.FindPermissionOutsideScope(requestedPermissions, actorPermissions, unrestricted);
        if (outsideScope is not null)
            return $"ROLE_GRANT_OUT_OF_SCOPE: actor cannot grant permission '{outsideScope}'.";

        var targetFacilities = await db.UserFacilities.AsNoTracking()
            .Where(membership => membership.UserId == targetUserId && membership.IsActive)
            .Select(membership => membership.FacilityId)
            .ToArrayAsync(ct);
        var actorId = GetActorId(actor);
        var actorFacilities = actorId is null
            ? Array.Empty<string>()
            : await db.UserFacilities.AsNoTracking()
                .Where(membership => membership.UserId == actorId && membership.IsActive)
                .Select(membership => membership.FacilityId)
                .ToArrayAsync(ct);
        var crossFacility = unrestricted || actorPermissions.Contains("facility.cross", StringComparer.OrdinalIgnoreCase);
        if (!RoleGovernanceRules.IsFacilityScopeAllowed(targetFacilities, actorFacilities, crossFacility))
            return "FACILITY_SCOPE_DENIED: actor cannot assign roles outside the actor's facility scope.";

        return null;
    }

    private static async Task<HashSet<string>> GetActorPermissionsAsync(
        IApplicationDbContext db,
        ClaimsPrincipal actor,
        CancellationToken ct)
    {
        var permissions = actor.FindAll(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Permissions)
            .SelectMany(claim => claim.Value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (permissions.Count > 0) return permissions;

        var actorId = GetActorId(actor);
        if (actorId is null) return permissions;

        var fromDb = await (
            from userRole in db.UserRoles.AsNoTracking()
            join rolePermission in db.RolePermissions.AsNoTracking() on userRole.RoleId equals rolePermission.RoleId
            where userRole.UserId == actorId.Value
            select rolePermission.PermissionCode).ToListAsync(ct);
        permissions.UnionWith(fromDb);
        return permissions;
    }

    private static Guid? GetActorId(ClaimsPrincipal actor)
    {
        var value = actor.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value
            ?? actor.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
