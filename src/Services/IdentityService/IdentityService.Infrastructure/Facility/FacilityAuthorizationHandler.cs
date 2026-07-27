using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Infrastructure.Facility;

/// <summary>
/// Authorization requirement that enforces facility-level access control.
/// Users must either:
///   - Have cross-facility privileges (Admin/SuperAdmin role or "facility:cross" permission), OR
///   - Have a facility_id that matches the requested resource's facility
/// </summary>
public class FacilityRequirement : IAuthorizationRequirement
{
    public bool StrictMode { get; }
    public FacilityRequirement(bool strictMode = false) { StrictMode = strictMode; }
}

/// <summary>
/// Authorization handler for FacilityRequirement.
/// Uses IHttpContextAccessor to resolve the scoped FacilityContext per-request.
/// This pattern is required because IAuthorizationHandler is singleton,
/// but FacilityContext is scoped.
/// </summary>
public class FacilityAuthorizationHandler : AuthorizationHandler<FacilityRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<FacilityAuthorizationHandler> _logger;

    public FacilityAuthorizationHandler(IHttpContextAccessor httpContextAccessor, ILogger<FacilityAuthorizationHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, FacilityRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            context.Succeed(requirement); // No HTTP context — allow (e.g., background jobs)
            return Task.CompletedTask;
        }

        var facilityContext = httpContext.RequestServices.GetRequiredService<FacilityContext>();

        // Cross-facility users always pass (unless strict mode)
        if (facilityContext.IsCrossFacility && !requirement.StrictMode)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // User must have a facility assigned
        if (string.IsNullOrWhiteSpace(facilityContext.FacilityId))
        {
            _logger.LogWarning("Facility access denied: user has no facility assigned");
            context.Fail(new AuthorizationFailureReason(this, "User has no facility assigned."));
            return Task.CompletedTask;
        }

        // Try to get the target facility from route data or request context
        var targetFacility = GetTargetFacility(context);

        if (string.IsNullOrWhiteSpace(targetFacility))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (string.Equals(facilityContext.FacilityId, targetFacility, StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (facilityContext.AuthorizedFacilities.Any(f =>
                string.Equals(f, targetFacility, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        _logger.LogWarning(
            "Facility access denied: User facility={UserFacility}, Target={TargetFacility}",
            facilityContext.FacilityId, targetFacility);

        context.Fail(new AuthorizationFailureReason(this,
            $"User facility '{facilityContext.FacilityId}' does not match target '{targetFacility}'."));

        return Task.CompletedTask;
    }

    private static string? GetTargetFacility(AuthorizationHandlerContext context)
    {
        if (context.Resource is HttpContext httpContext)
        {
            if (httpContext.Request.RouteValues.TryGetValue("facilityId", out var facilityObj)
                && facilityObj is string facilityStr)
                return facilityStr;

            var queryFacility = httpContext.Request.Query["facilityId"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(queryFacility))
                return queryFacility;

            var headerFacility = httpContext.Request.Headers["X-Facility-Id"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerFacility))
                return headerFacility;
        }
        return null;
    }
}
