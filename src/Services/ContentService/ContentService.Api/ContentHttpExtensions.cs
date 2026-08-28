using System.Security.Claims;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.SharedKernel.Authorization;

namespace His.Hope.ContentService.Api;

internal static class ContentHttpExtensions
{
    public const string DefaultTenantKey = "customer-factory-x";

    public static string? GetUserId(this ClaimsPrincipal user) =>
        user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public static bool HasPermission(this ClaimsPrincipal user, string permissionCode) =>
        user.FindAll("permissions")
            .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Any(value => string.Equals(value, permissionCode, StringComparison.OrdinalIgnoreCase));

    public static string ResolvePublicTenant(HttpContext context)
    {
        var requested = context.GetRequestedTenant();
        if (string.IsNullOrWhiteSpace(requested))
            return DefaultTenantKey;

        // Public endpoints must never become an arbitrary tenant data oracle.
        // Keep the default tenant as the safe fallback and allow explicit
        // publishing of additional public tenants through configuration.
        var allowList = context.RequestServices
            .GetRequiredService<IConfiguration>()
            .GetSection("Content:PublicTenantAllowlist")
            .Get<string[]>()
            ?? [];
        var isAllowed = string.Equals(requested, DefaultTenantKey, StringComparison.OrdinalIgnoreCase)
            || allowList.Any(value => string.Equals(value?.Trim(), requested, StringComparison.OrdinalIgnoreCase));

        return isAllowed ? requested : DefaultTenantKey;
    }

    public static string? ResolveManageTenant(HttpContext context, bool isMutation = false)
    {
        var user = context.User;
        if (!user.Identity?.IsAuthenticated ?? true)
            return null;

        var requested = context.GetRequestedTenant();
        if (!user.TryResolveActiveTenant(requested, out var tenantKey, AllowContentCrossTenant))
            return null;

        if (isMutation && !string.Equals(tenantKey, user.GetTokenTenant(), StringComparison.OrdinalIgnoreCase))
            context.Items["content.crossTenantWrite"] = true;

        return tenantKey;
    }

    private static bool AllowContentCrossTenant(ClaimsPrincipal user, string requestedTenant, string tokenTenant) =>
        string.Equals(user.GetPortalClass(), "operator", StringComparison.OrdinalIgnoreCase) &&
        user.HasPermission(HisHopePermissions.Content.Manage);

}
