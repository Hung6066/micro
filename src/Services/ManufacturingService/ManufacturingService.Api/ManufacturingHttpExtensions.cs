using System.Security.Claims;

internal static class ManufacturingHttpExtensions
{
    public static string? GetTokenTenant(this ClaimsPrincipal user) =>
        user.FindFirst("tenant_id")?.Value ?? user.FindFirst("tenant")?.Value;

    public static string? GetPortalClass(this ClaimsPrincipal user) =>
        user.FindFirst("portal_class")?.Value;

    /// <summary>
    /// Resolves the active manufacturing tenant from JWT, optionally overridden by
    /// <c>?tenantKey=</c> for operator portals (ADR 017 cross-tenant support).
    /// </summary>
    public static string? ResolveActiveTenant(HttpContext context)
    {
        var requested = context.Request.Query["tenantKey"].FirstOrDefault()?.Trim();
        return TryResolveTenant(context, requested, out var tenantKey) ? tenantKey : null;
    }

    public static bool TryResolveTenant(HttpContext context, string? requestedTenant, out string tenantKey)
    {
        var user = context.User;
        var tokenTenant = user.GetTokenTenant();
        if (string.IsNullOrWhiteSpace(tokenTenant))
        {
            tenantKey = string.Empty;
            return false;
        }

        var requested = requestedTenant?.Trim();
        if (string.IsNullOrWhiteSpace(requested) ||
            string.Equals(requested, tokenTenant, StringComparison.OrdinalIgnoreCase))
        {
            tenantKey = tokenTenant;
            return true;
        }

        if (string.Equals(user.GetPortalClass(), "operator", StringComparison.OrdinalIgnoreCase))
        {
            tenantKey = requested;
            return true;
        }

        tenantKey = string.Empty;
        return false;
    }

    public static bool TenantMatches(HttpContext context, string requestedTenant)
    {
        if (!TryResolveTenant(context, requestedTenant, out var scopedTenant))
            return false;

        return string.Equals(scopedTenant, requestedTenant, StringComparison.OrdinalIgnoreCase);
    }
}
