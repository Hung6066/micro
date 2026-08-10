using Microsoft.AspNetCore.Authorization;

namespace His.Hope.IdentityService.Infrastructure.Facility;

/// <summary>
/// Authorization attribute that requires facility-level access control.
/// Apply to endpoints or controllers that should be scoped to a specific facility.
/// 
/// Usage:
///   [RequireFacility]           — user must have any facility assigned
///   [RequireFacility(Strict = true)]  — cross-facility users also need explicit facility match
/// </summary>
public class RequireFacilityAttribute : AuthorizeAttribute
{
    private const string FacilityPolicyPrefix = "Facility";

    public RequireFacilityAttribute(bool strict = false)
    {
        Policy = strict ? $"{FacilityPolicyPrefix}:Strict" : FacilityPolicyPrefix;
    }

    /// <summary>
    /// When true, even cross-facility users (Admins) must have an explicit facility match.
    /// </summary>
    public bool Strict
    {
        get => Policy == $"{FacilityPolicyPrefix}:Strict";
        init => Policy = value ? $"{FacilityPolicyPrefix}:Strict" : FacilityPolicyPrefix;
    }
}
