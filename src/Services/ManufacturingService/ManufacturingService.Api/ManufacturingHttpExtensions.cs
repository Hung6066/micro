using His.Hope.AspNetCore.Tenancy;

internal static class ManufacturingHttpExtensions
{
    public static string? ResolveActiveTenant(HttpContext context) =>
        context.ResolveActiveTenant();

    public static bool TryResolveTenant(HttpContext context, string? requestedTenant, out string tenantKey)
    {
        // Resolve through the shared context resolver so the canonical
        // X-HisHope-Tenant header is honored consistently. The optional
        // parameter remains only for legacy endpoint model binding.
        var requested = context.GetRequestedTenant();
        if (string.IsNullOrWhiteSpace(requested) && !string.IsNullOrWhiteSpace(requestedTenant))
            requested = requestedTenant.Trim();

        return context.User.TryResolveActiveTenant(requested, out tenantKey);
    }

    public static bool TenantMatches(HttpContext context, string requestedTenant) =>
        context.TenantMatches(requestedTenant);
}
