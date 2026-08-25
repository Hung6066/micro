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

    public static string? GetEmail(this ClaimsPrincipal user) =>
        user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value ?? "buyer@example.com";

    public static bool HasPermission(this ClaimsPrincipal user, string permissionCode)
    {
        return user.FindAll("permissions")
            .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Any(value => string.Equals(value, permissionCode, StringComparison.OrdinalIgnoreCase));
    }

    public static string? ResolveCommerceTenant(HttpContext context)
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
        if (string.Equals(portalClass, "operator", StringComparison.OrdinalIgnoreCase) &&
            user.HasPermission(HisHopePermissions.Commerce.OrdersView))
            return requestedTenant;

        return null;
    }
}
