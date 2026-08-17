namespace His.Hope.SharedKernel.Authorization;

/// <summary>
/// Stable authorization vocabulary shared by token issuance, resource APIs and
/// policy registration.  The server remains the source of truth; clients only
/// consume the resulting capabilities.
/// </summary>
public static class AuthorizationConstants
{
    public static class GrantTypes
    {
        public const string TokenExchange = "urn:ietf:params:oauth:grant-type:token-exchange";
    }

    public static class Claims
    {
        public const string PrincipalType = "principal_type";
    }

    public static class PrincipalTypes
    {
        public const string Human = "human";
        public const string Group = "group";
        public const string Workload = "workload";
        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Human, Group, Workload };
    }

    public static class ScopeKinds
    {
        public const string Organization = "organization";
        public const string Tenant = "tenant";
        public const string Account = "account";
        public const string Environment = "environment";
        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
        { Organization, Tenant, Account, Environment };
    }

    public static class LifecycleStatuses
    {
        public const string Draft = "draft";
        public const string Published = "published";
        public const string Active = "active";
    }

    public static class Policies
    {
        public const string HumanAdmin = "HumanAdmin";
    }
}
