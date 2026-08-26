using System.Security.Claims;
using His.Hope.SharedKernel.Authorization;

namespace His.Hope.CommerceService.Api;

internal static class CommerceHttpExtensions
{
    public static string? GetUserId(this ClaimsPrincipal user) =>
        user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public static string? GetTokenTenant(this ClaimsPrincipal user) =>
        user.FindFirst("tenant_id")?.Value ?? user.FindFirst("tenant")?.Value;

    public static string? GetPortalClass(this ClaimsPrincipal user) =>
        user.FindFirst("portal_class")?.Value;

    public static string? GetClientId(this ClaimsPrincipal user) =>
        user.FindFirst("client_id")?.Value ?? user.FindFirst("azp")?.Value;

    public static string GetEmail(this ClaimsPrincipal user) =>
        user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value ?? "buyer@example.com";

    public static bool HasPermission(this ClaimsPrincipal user, string permissionCode) =>
        user.FindAll("permissions")
            .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Any(value => string.Equals(value, permissionCode, StringComparison.OrdinalIgnoreCase));

    public static bool IsCrossTenant(ClaimsPrincipal user, string resolvedTenant)
    {
        var tokenTenant = user.GetTokenTenant();
        return !string.IsNullOrWhiteSpace(tokenTenant) &&
               !string.Equals(tokenTenant, resolvedTenant, StringComparison.OrdinalIgnoreCase);
    }

    public static string? ResolveCommerceTenant(HttpContext context, bool isMutation = false)
    {
        var user = context.User;
        var tokenTenant = user.GetTokenTenant();
        if (string.IsNullOrWhiteSpace(tokenTenant))
            return null;

        var requestedTenant = context.Request.Query["tenantKey"].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(requestedTenant) ||
            string.Equals(requestedTenant, tokenTenant, StringComparison.OrdinalIgnoreCase))
            return tokenTenant;

        var portalClass = user.GetPortalClass();
        if (!string.Equals(portalClass, "operator", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!user.HasPermission(HisHopePermissions.Commerce.OrdersView))
            return null;

        if (isMutation)
            context.Items["commerce.crossTenantWrite"] = true;

        return requestedTenant;
    }
}
