using System.Security.Claims;

namespace His.Hope.Authorization;

/// <summary>
/// Trusted attributes used by a resource authorization decision. Resource
/// attributes must be loaded by the owning service, never copied from an
/// untrusted request body.
/// </summary>
public sealed record AuthorizationResource(
    string Type,
    string CanonicalId,
    string? TenantId = null,
    string? FacilityId = null,
    string? Sensitivity = null,
    string? LifecycleState = null);

/// <summary>
/// Immutable input to the shared authorization evaluator.
/// </summary>
public sealed record AuthorizationContext(
    ClaimsPrincipal Principal,
    string Action,
    AuthorizationResource? Resource = null,
    string? PurposeOfUse = null,
    string? DevicePosture = null,
    string? EmergencyReason = null,
    bool RequireResource = false,
    bool ResourceLookupFailed = false);
