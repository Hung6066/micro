using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Infrastructure.Facility;

/// <summary>
/// ASP.NET Core middleware that resolves the current user's facility context
/// from JWT claims and makes it available via FacilityContext for the request.
/// 
/// Extracts:
///   - facility_id claim → FacilityContext.FacilityId
///   - Cross-facility role check via "Admin" role or "facility:cross" permission
///   - facility_ids claim (comma-separated) → AuthorizedFacilities for multi-facility users
/// </summary>
public class FacilityResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<FacilityResolutionMiddleware> _logger;

    private static readonly HashSet<string> CrossFacilityRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin", "SuperAdmin", "SystemAdmin"
    };

    public FacilityResolutionMiddleware(RequestDelegate next, ILogger<FacilityResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, FacilityContext facilityContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // Extract facility from JWT claims
            var facilityClaim = context.User.FindFirst("facility_id")?.Value;
            var isCrossFacility = IsCrossFacilityUser(context.User);

            facilityContext.FacilityId = facilityClaim;
            facilityContext.IsCrossFacility = isCrossFacility;

            // Extract authorized facilities for multi-facility users
            var facilityIdsClaim = context.User.FindFirst("facility_ids")?.Value;
            if (!string.IsNullOrWhiteSpace(facilityIdsClaim))
            {
                facilityContext.AuthorizedFacilities = facilityIdsClaim
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
            }

            // If no facility claim and not cross-facility, warn
            if (string.IsNullOrWhiteSpace(facilityClaim) && !isCrossFacility)
            {
                _logger.LogWarning(
                    "Authenticated user {UserId} has no facility_id claim and is not cross-facility. Access may be restricted.",
                    context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            }

            _logger.LogDebug(
                "Facility resolved: UserId={UserId}, FacilityId={FacilityId}, IsCrossFacility={IsCrossFacility}",
                context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                facilityContext.FacilityId ?? "none",
                facilityContext.IsCrossFacility);
        }

        await _next(context);
    }

    private static bool IsCrossFacilityUser(ClaimsPrincipal user)
    {
        // Check cross-facility permission
        if (user.HasClaim("permissions", "facility:cross"))
            return true;

        // Check admin/super-admin roles
        var roleClaims = user.FindAll(ClaimTypes.Role).Select(c => c.Value);
        if (roleClaims.Any(r => CrossFacilityRoles.Contains(r)))
            return true;

        return false;
    }
}

/// <summary>
/// Extension method to register facility resolution middleware in the pipeline.
/// Must be placed AFTER Authentication middleware and BEFORE Authorization middleware.
/// </summary>
public static class FacilityResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseFacilityResolution(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<FacilityResolutionMiddleware>();
    }
}
