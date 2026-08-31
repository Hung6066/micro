using System.Security.Claims;
using His.Hope.AspNetCore.Tenancy;
using His.Hope.SharedKernel.Authorization;
using His.Hope.SharedKernel.Protocol;

namespace His.Hope.CommerceService.Api;

internal static class CommerceHttpExtensions
{
    public static string? GetUserId(this ClaimsPrincipal user) =>
        user.FindFirst(HisHopeProtocolConstants.Claims.Subject)?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public static string? GetClientId(this ClaimsPrincipal user) =>
        user.FindFirst(HisHopeProtocolConstants.Claims.ClientId)?.Value ?? user.FindFirst(HisHopeProtocolConstants.Claims.AuthorizedParty)?.Value;

    public static string GetEmail(this ClaimsPrincipal user) =>
        user.FindFirst(HisHopeProtocolConstants.Claims.Email)?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value ?? "buyer@example.com";

    public static bool HasPermission(this ClaimsPrincipal user, string permissionCode) =>
        user.FindAll(HisHopeProtocolConstants.Claims.Permissions)
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
        var tenantKey = context.RequestServices.GetService<IHisHopeTenantContext>()?.TenantKey
            ?? context.ResolveActiveTenant(AllowCommerceCrossTenant);
        if (string.IsNullOrWhiteSpace(tenantKey))
            return null;

        if (isMutation && IsCrossTenant(context.User, tenantKey))
            context.Items["commerce.crossTenantWrite"] = true;

        return tenantKey;
    }

    private static bool AllowCommerceCrossTenant(ClaimsPrincipal user, string requestedTenant, string tokenTenant) =>
        string.Equals(user.GetPortalClass(), HisHopeProtocolConstants.PortalClasses.Operator, StringComparison.OrdinalIgnoreCase) &&
        user.HasPermission(HisHopePermissions.Commerce.OrdersView);
}
