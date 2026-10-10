namespace His.Hope.IdentityService.Application.Conglomerate;

public sealed class ConglomerateOptions
{
    public const string SectionName = "Conglomerate";

    public bool Enabled { get; set; }

    public bool SkipDemoHospitalScope { get; set; } = true;

    public bool SeedPilotMemberships { get; set; } = true;

    public bool SeedPilotUsers { get; set; } = true;

    public bool ResetPilotPasswords { get; set; }

    public string? SeedDataPath { get; set; }

    public string? PilotUserPassword { get; set; }

    public string? IamScopesPath { get; set; }

    public string? OidcClientsPath { get; set; }

    /// <summary>ADR 017: optional customer tenant definitions merged at startup.</summary>
    public string? CustomerTenantsPath { get; set; }

    /// <summary>ADR 017: when <c>all</c>, HQ operators may list customer tenants without scopeId.</summary>
    public string HqCustomerVisibility { get; set; } = ConglomerateConstants.HqCustomerVisibilityNone;

    public ConglomerateOrganizationOptions Organization { get; set; } = new();

    public List<ConglomerateTenantOptions> Tenants { get; set; } = [];

    public Dictionary<string, string> OidcClientTenants { get; set; } =
        new(StringComparer.Ordinal);

    public Dictionary<string, string> OidcClientDisplayNames { get; set; } =
        new(StringComparer.Ordinal);

    public Dictionary<string, string> OidcClientPortalClasses { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public CrossTenantPolicyOptions CrossTenantPolicy { get; set; } = new();
}

public sealed class ConglomerateOrganizationOptions
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}

public sealed class ConglomerateTenantOptions
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string TenantClass { get; set; } = ConglomerateConstants.TenantClassInternal;

    public string? OperatorHome { get; set; }

    public string? AccountKey { get; set; }

    public string? AccountDisplayName { get; set; }

    public string? EnvironmentKey { get; set; }

    public string? EnvironmentDisplayName { get; set; }

    public string? ContractId { get; set; }

    public string? DataRegion { get; set; }
}

public sealed class CrossTenantPolicyOptions
{
    public bool DefaultDeny { get; set; } = true;

    public List<CrossTenantAllowedPairOptions> AllowedPairs { get; set; } = [];
}

public sealed class CrossTenantAllowedPairOptions
{
    public string Source { get; set; } = string.Empty;

    public string? Target { get; set; }

    public string? TargetClass { get; set; }

    public bool OperatorHomeMatch { get; set; }

    public bool RequiresJit { get; set; }

    public int MaxDurationMinutes { get; set; } = 60;

    public string Reason { get; set; } = string.Empty;

    public List<string> Permissions { get; set; } = [];
}
