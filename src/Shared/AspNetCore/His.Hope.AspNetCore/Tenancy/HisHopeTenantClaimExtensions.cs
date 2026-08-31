using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using His.Hope.SharedKernel.Protocol;

namespace His.Hope.AspNetCore.Tenancy;

/// <summary>
/// Canonical tenant resolution for His.Hope APIs (ADR 017 cross-tenant operator support).
/// </summary>
public static class HisHopeTenantClaimExtensions
{
    public static string? GetRequestedTenant(this HttpContext context) =>
        context.Request.Headers["X-HisHope-Tenant"].FirstOrDefault()?.Trim()
        ?? context.Request.Query["tenantKey"].FirstOrDefault()?.Trim();

    /// <summary>
    /// Returns true when the canonical header and legacy query selector both
    /// exist but disagree. Ambiguous tenant selectors are rejected rather than
    /// silently resolved by precedence.
    /// </summary>
    public static bool HasConflictingTenantSelectors(this HttpContext context)
    {
        var header = context.Request.Headers["X-HisHope-Tenant"].FirstOrDefault()?.Trim();
        var query = context.Request.Query["tenantKey"].FirstOrDefault()?.Trim();
        return !string.IsNullOrWhiteSpace(header) && !string.IsNullOrWhiteSpace(query) &&
               !string.Equals(header, query, StringComparison.OrdinalIgnoreCase);
    }

    public static string? GetTokenTenant(this ClaimsPrincipal user) =>
        user.FindFirst(HisHopeProtocolConstants.Claims.TenantId)?.Value ??
        user.FindFirst(HisHopeProtocolConstants.Claims.Tenant)?.Value;

    public static string? GetPortalClass(this ClaimsPrincipal user) =>
        user.FindFirst(HisHopeProtocolConstants.Claims.PortalClass)?.Value;

    public static IReadOnlySet<string> GetAllowedTenants(this ClaimsPrincipal user)
    {
        var values = user.Claims
            .Where(claim => claim.Type is HisHopeProtocolConstants.Claims.TenantId or
                HisHopeProtocolConstants.Claims.Tenant or HisHopeProtocolConstants.Claims.TenantMembership or
                "tenant_memberships" or "tenant_ids" or "tenants")
            .SelectMany(claim => ExpandTenantClaim(claim.Value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return values;
    }

    /// <summary>
    /// Resolves the active tenant from JWT, optionally overridden by the canonical
    /// <c>X-HisHope-Tenant</c> header. The legacy <c>?tenantKey=</c> query parameter
    /// remains supported during contract migration.
    /// </summary>
    public static string? ResolveActiveTenant(
        this HttpContext context,
        Func<ClaimsPrincipal, string, string, bool>? allowCrossTenant = null)
    {
        if (context.HasConflictingTenantSelectors())
            return null;

        var requested = context.GetRequestedTenant();
        return context.User.TryResolveActiveTenant(requested, out var tenantKey, allowCrossTenant)
            ? tenantKey
            : null;
    }

    public static bool TryResolveActiveTenant(
        this ClaimsPrincipal user,
        string? requestedTenant,
        out string tenantKey,
        Func<ClaimsPrincipal, string, string, bool>? allowCrossTenant = null)
    {
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

        if (allowCrossTenant?.Invoke(user, requested, tokenTenant) == true ||
            IsOperatorMembershipCrossTenant(user, requested))
        {
            tenantKey = requested;
            return true;
        }

        tenantKey = string.Empty;
        return false;
    }

    public static bool TenantMatches(
        this HttpContext context,
        string requestedTenant,
        Func<ClaimsPrincipal, string, string, bool>? allowCrossTenant = null) =>
        context.User.TryResolveActiveTenant(requestedTenant, out var scopedTenant, allowCrossTenant) &&
        string.Equals(scopedTenant, requestedTenant, StringComparison.OrdinalIgnoreCase);

    public static bool IsOperatorMembershipCrossTenant(ClaimsPrincipal user, string requestedTenant) =>
        string.Equals(user.GetPortalClass(), HisHopeProtocolConstants.PortalClasses.Operator, StringComparison.OrdinalIgnoreCase) &&
        user.GetAllowedTenants().Contains(requestedTenant);

    private static IEnumerable<string> ExpandTenantClaim(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            yield break;

        if (normalized.Length > 0 && normalized[0] == '[')
        {
            JsonElement[]? items = null;
            try
            {
                items = JsonSerializer.Deserialize<JsonElement[]>(normalized);
            }
            catch (JsonException)
            {
            }

            if (items is not null)
            {
                foreach (var item in items)
                {
                    if (item.ValueKind == JsonValueKind.String && item.GetString() is { } text)
                        yield return text;
                    else if (item.ValueKind == JsonValueKind.Object &&
                             item.TryGetProperty(HisHopeProtocolConstants.Claims.TenantId, out var tenant) &&
                             tenant.ValueKind == JsonValueKind.String &&
                             tenant.GetString() is { } key)
                        yield return key;
                }

                yield break;
            }
        }

        foreach (var item in normalized.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return item;
    }
}
