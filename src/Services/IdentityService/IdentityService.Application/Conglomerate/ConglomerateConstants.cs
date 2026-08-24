namespace His.Hope.IdentityService.Application.Conglomerate;

public static class ConglomerateConstants
{
    public const string TenantClassInternal = "internal";
    public const string TenantClassCustomer = "customer";

    public const string PortalClassOperator = "operator";
    public const string PortalClassCustomerOperator = "customer_operator";
    public const string PortalClassEndUser = "end_user";

    public const string ClaimPortalClass = "portal_class";
    public const string ClaimTenantClass = "tenant_class";

    public const string HqCustomerVisibilityAll = "all";
    public const string HqCustomerVisibilityNone = "none";

    public const string SupportElevationHeader = "X-Support-Elevation-Id";
}
