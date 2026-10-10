namespace His.Hope.AspNetCore.Tenancy;

/// <summary>
/// Resolved tenant context for the current HTTP request. Data-plane handlers
/// should consume this context instead of binding a tenant selector from a
/// request body or query string.
/// </summary>
public interface IHisHopeTenantContext
{
    string? TenantKey { get; }
    bool HasTenant { get; }
}

internal sealed class HisHopeTenantContext : IHisHopeTenantContext
{
    public string? TenantKey { get; private set; }
    public bool HasTenant => !string.IsNullOrWhiteSpace(TenantKey);

    internal void Set(string? tenantKey) =>
        TenantKey = string.IsNullOrWhiteSpace(tenantKey) ? null : tenantKey.Trim();
}
