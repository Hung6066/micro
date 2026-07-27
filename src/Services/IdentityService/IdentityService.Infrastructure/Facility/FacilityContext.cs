namespace His.Hope.IdentityService.Infrastructure.Facility;

/// <summary>
/// Scoped service that holds the current user's resolved facility context.
/// Extracted from JWT claims by FacilityResolutionMiddleware.
/// Thread-safe for the lifetime of a single HTTP request.
/// </summary>
public class FacilityContext
{
    /// <summary>
    /// Current user's primary facility ID (from JWT facility_id claim).
    /// Null if the user is a super-admin with cross-facility access.
    /// </summary>
    public string? FacilityId { get; set; }

    /// <summary>
    /// True if the user has cross-facility (system-wide) access privileges.
    /// Cross-facility users bypass per-facility query filters.
    /// </summary>
    public bool IsCrossFacility { get; set; }

    /// <summary>
    /// List of facility IDs the user is authorized to access.
    /// Populated for users with multi-facility roles.
    /// If empty and FacilityId is null → no facility restriction.
    /// </summary>
    public List<string> AuthorizedFacilities { get; set; } = new();

    /// <summary>
    /// Ensures the context has been resolved. Throws if middleware didn't run.
    /// </summary>
    public bool IsResolved => FacilityId != null || IsCrossFacility;
}
