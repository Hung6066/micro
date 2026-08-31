namespace His.Hope.SharedKernel.Protocol;

/// <summary>
/// Stable wire-level values shared by HTTP, BFF and service adapters.
/// Domain states and user-facing text belong to their owning bounded context.
/// </summary>
public static class HisHopeProtocolConstants
{
    public static class Claims
    {
        public const string Subject = "sub";
        public const string ClientId = "client_id";
        public const string AuthorizedParty = "azp";
        public const string Email = "email";
        public const string Name = "name";
        public const string GivenName = "given_name";
        public const string FamilyName = "family_name";
        public const string AuthenticationMethod = "amr";
        public const string AuthenticationContext = "acr";
        public const string TenantId = "tenant_id";
        public const string Tenant = "tenant";
        public const string TenantMembership = "tenant_membership";
        public const string Permissions = "permissions";
        public const string CorrelationId = "correlationId";
        public const string PortalClass = "portal_class";
        public const string AuthorizationConstraints = "authorization_constraints";
        public const string FacilityId = "facility_id";
        public const string SuperAdmin = "super_admin";
        public const string TenantClass = "tenant_class";
    }

    public static class Headers
    {
        public const string Authorization = "Authorization";
        public const string Dpop = "DPoP";
        public const string ContentType = "Content-Type";
        public const string Accept = "Accept";
        public const string CorrelationId = "X-Correlation-ID";
        public const string CsrfToken = "X-CSRF-Token";
        public const string SupportElevationId = "X-Support-Elevation-Id";
        public const string RequestedWith = "X-Requested-With";
        public const string Timezone = "X-Timezone";
        public const string Currency = "X-Currency";
        public const string AcceptLanguage = "Accept-Language";
        public const string EntityTag = "ETag";
    }

    public static class Cookies
    {
        public const string BrowserSession = "hishop_sid";
        public const string OidcMfa = "hishop_oidc_mfa";
        public const string OidcMfaSession = "hishop_oidc_mfa_session";
        public const string TrustedDevice = "hishop_trusted_device";
        public const string BrowserCsrf = "hishop_csrf";
    }

    public static class MediaTypes
    {
        public const string Json = "application/json";
        public const string ProblemJson = "application/problem+json";
    }

    public static class PortalClasses
    {
        public const string Operator = "operator";
    }

    public static class Routes
    {
        public const string Health = "/health";
        public const string Live = "/health/live";
        public const string Ready = "/health/ready";
    }

    public static class AuthorizationSchemes
    {
        public const string Bearer = "Bearer";
        public const string Dpop = "DPoP";
    }

    public static class Messaging
    {
        public const string DeadLetterExchange = "his-hope.dlx";
    }
}
