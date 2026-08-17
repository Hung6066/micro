namespace His.Hope.IdentityService.Application.Authorization;

/// <summary>
/// Pure governance rules shared by Identity API role and grant workflows.
/// The API remains responsible for loading current roles, permissions and
/// facility memberships before applying these rules.
/// </summary>
public static class RoleGovernanceRules
{
    public static string[] NormalizePermissionCodes(IEnumerable<string>? permissionCodes) =>
        (permissionCodes ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static string? FindPermissionOutsideScope(
        IEnumerable<string> requestedPermissions,
        IEnumerable<string> actorPermissions,
        bool unrestricted) 
    {
        if (unrestricted) return null;

        var allowed = actorPermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return requestedPermissions.FirstOrDefault(permission => !allowed.Contains(permission));
    }

    public static bool IsFacilityScopeAllowed(
        IEnumerable<string> targetFacilities,
        IEnumerable<string> actorFacilities,
        bool crossFacility)
    {
        if (crossFacility) return true;

        var actor = actorFacilities
            .Where(facility => !string.IsNullOrWhiteSpace(facility))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return targetFacilities
            .Where(facility => !string.IsNullOrWhiteSpace(facility))
            .All(actor.Contains);
    }
}
