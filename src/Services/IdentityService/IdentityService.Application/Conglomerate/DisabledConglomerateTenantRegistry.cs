namespace His.Hope.IdentityService.Application.Conglomerate;

public sealed class DisabledConglomerateTenantRegistry : IConglomerateTenantRegistry
{
    public bool IsEnabled => false;

    public string HqCustomerVisibility => ConglomerateConstants.HqCustomerVisibilityNone;

    public bool IsConglomerateClient(string? clientId) => false;

    public string? GetClientTenant(string? clientId) => null;

    public string GetPortalClass(string? clientId) => ConglomerateConstants.PortalClassOperator;

    public string GetTenantClass(string tenantKey) => ConglomerateConstants.TenantClassInternal;

    public string? GetOperatorHome(string tenantKey) => null;

    public IReadOnlyList<string> GetClientIdsForTenant(string tenantKey) => [];

    public IReadOnlyList<string> GetCustomerTenantsForOperator(string operatorTenantKey) => [];

    public bool IsCustomerTenant(string tenantKey) => false;

    public IReadOnlyList<CrossTenantAllowedPairOptions> AllowedCrossTenantPairs { get; } = [];

    public ConglomerateTenantOptions? GetTenantProfile(string tenantKey) => null;
}
