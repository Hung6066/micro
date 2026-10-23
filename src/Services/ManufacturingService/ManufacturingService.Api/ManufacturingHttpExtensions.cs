using His.Hope.AspNetCore.Tenancy;

internal static class ManufacturingHttpExtensions
{
    public static string? ResolveActiveTenant(HttpContext context) =>
        context.ResolveActiveTenant();

    public static bool TryResolveTenant(HttpContext context, string? requestedTenant, out string tenantKey)
    {
        // The endpoint boundary resolves the tenant once. Keep the optional
        // parameter solely as a compatibility check; handlers never select a
        // tenant independently from the request context.
        if (context.HasConflictingTenantSelectors())
        {
            tenantKey = string.Empty;
            return false;
        }

        tenantKey = context.RequestServices.GetService<IHisHopeTenantContext>()?.TenantKey
            ?? context.ResolveActiveTenant()
            ?? string.Empty;
        return !string.IsNullOrWhiteSpace(tenantKey) &&
            (string.IsNullOrWhiteSpace(requestedTenant) ||
             string.Equals(requestedTenant.Trim(), tenantKey, StringComparison.OrdinalIgnoreCase));
    }

    public static bool TenantMatches(HttpContext context, string requestedTenant) =>
        context.TenantMatches(requestedTenant);
}
