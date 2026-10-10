using System.Security.Claims;

namespace His.Hope.Authorization;

/// <summary>
/// Immutable facility boundary derived from an already authenticated token.
/// Services should use this scope to constrain resource queries and command
/// targets; a permission check alone is not a row-level authorization check.
/// </summary>
public sealed record FacilityAccessScope(
    IReadOnlySet<string> FacilityIds,
    bool IsCrossFacility,
    bool IsEnforced = true)
{
    public bool CanAccess(string? facilityId)
    {
        if (string.IsNullOrWhiteSpace(facilityId)) return false;
        return IsCrossFacility || FacilityIds.Contains(facilityId.Trim());
    }

    public static FacilityAccessScope FromPrincipal(ClaimsPrincipal principal)
    {
        var facilityIds = principal.FindAll("facility_ids")
            .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Concat(principal.FindAll("facility_id").Select(claim => claim.Value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var isCrossFacility = principal.FindAll(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Permissions)
            .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(permission => string.Equals(permission, His.Hope.SharedKernel.Authorization.HisHopePermissions.Facilities.Cross, StringComparison.OrdinalIgnoreCase));

        return new FacilityAccessScope(facilityIds, isCrossFacility, true);
    }
}
