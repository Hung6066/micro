using System.Security.Claims;
using His.Hope.SharedKernel.Protocol;

namespace His.Hope.Authorization;

internal static class TenantAccessEvaluator
{
    public static string? Evaluate(
        ClaimsPrincipal principal,
        AuthorizationResource resource,
        string action,
        ICrossTenantAccessPolicy? crossTenantPolicy = null)
    {
        var resourceTenant = resource.TenantId;
        if (string.IsNullOrWhiteSpace(resourceTenant))
            return null;

        var tokenTenant = principal.FindFirst(HisHopeProtocolConstants.Claims.TenantId)?.Value
            ?? principal.FindFirst(HisHopeProtocolConstants.Claims.Tenant)?.Value;
        if (string.IsNullOrWhiteSpace(tokenTenant))
            return "tenant_scope_denied";

        if (string.Equals(tokenTenant, resourceTenant, StringComparison.OrdinalIgnoreCase))
            return null;

        if (crossTenantPolicy?.IsCrossTenantAllowed(tokenTenant, resourceTenant, action) == true)
            return null;

        return "tenant_scope_denied";
    }
}
