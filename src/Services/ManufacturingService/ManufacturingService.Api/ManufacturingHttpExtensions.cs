using System.Security.Claims;
using System.Text.Json;

internal static class ManufacturingHttpExtensions
{
    public static string? GetTokenTenant(this ClaimsPrincipal user) =>
        user.FindFirst("tenant_id")?.Value ?? user.FindFirst("tenant")?.Value;

    private static IReadOnlySet<string> GetAllowedTenants(ClaimsPrincipal user)
    {
        var values = user.Claims
            .Where(claim => claim.Type is "tenant_id" or "tenant" or "tenant_membership" or "tenant_memberships" or "tenant_ids" or "tenants")
            .SelectMany(claim => ExpandTenantClaim(claim.Value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return values;
    }

    private static IEnumerable<string> ExpandTenantClaim(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) yield break;
        if (normalized.StartsWith("[", StringComparison.Ordinal))
        {
            JsonElement[]? items = null;
            try
            {
                items = JsonSerializer.Deserialize<JsonElement[]>(normalized);
            }
            catch (JsonException) { }
            if (items is not null)
            {
                foreach (var item in items)
                {
                    if (item.ValueKind == JsonValueKind.String && item.GetString() is { } text) yield return text;
                    else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("tenant_id", out var tenant) && tenant.ValueKind == JsonValueKind.String && tenant.GetString() is { } key) yield return key;
                }
                yield break;
            }
        }

        foreach (var item in normalized.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return item;
    }

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
            if (GetAllowedTenants(user).Contains(requested))
            {
                tenantKey = requested;
                return true;
            }
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
