using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.Authorization;

/// <summary>
/// EF boundary helpers shared by clinical services. Query filters are applied
/// before materialization and added rows are stamped from the authenticated
/// request, preventing a caller from selecting a facility outside its claims.
/// </summary>
public static class FacilityScopeEfExtensions
{
    public static FacilityAccessScope Resolve(IHttpContextAccessor? accessor)
    {
        var httpContext = accessor?.HttpContext;
        if (httpContext?.User.Identity?.IsAuthenticated != true)
            return new FacilityAccessScope(new HashSet<string>(StringComparer.OrdinalIgnoreCase), false, false);

        return FacilityAccessScope.FromPrincipal(httpContext.User);
    }

    public static void StampAddedFacilities(
        DbContext context,
        FacilityAccessScope scope,
        IHttpContextAccessor? accessor)
    {
        if (!scope.IsEnforced)
            return;

        var httpContext = accessor?.HttpContext;
        var requestedFacility = httpContext?.Request.Headers["X-Facility-Id"].FirstOrDefault();
        requestedFacility ??= httpContext?.User.FindFirst("facility_id")?.Value;
        if (string.IsNullOrWhiteSpace(requestedFacility) && scope.FacilityIds.Count == 1)
            requestedFacility = scope.FacilityIds.Single();

        if (!scope.CanAccess(requestedFacility))
            throw new UnauthorizedAccessException("A valid facility scope is required to create a resource.");

        foreach (var entry in context.ChangeTracker.Entries().Where(entry => entry.State == EntityState.Added))
        {
            var facilityProperty = entry.Metadata.FindProperty("FacilityId");
            if (facilityProperty is null)
                continue;

            var currentValue = entry.Property("FacilityId").CurrentValue as string;
            if (!string.IsNullOrWhiteSpace(currentValue) && !scope.CanAccess(currentValue))
                throw new UnauthorizedAccessException("The resource facility is outside the caller's facility scope.");

            entry.Property("FacilityId").CurrentValue = requestedFacility;
        }
    }
}
