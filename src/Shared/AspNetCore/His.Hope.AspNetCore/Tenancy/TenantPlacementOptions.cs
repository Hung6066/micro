namespace His.Hope.AspNetCore.Tenancy;

public sealed class TenantPlacementOptions
{
    public const string SectionName = "TenantPlacement";

    /// <summary>
    /// Master switch. When false, every tenant uses the service default connection
    /// regardless of placement entries (ADR 018).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Optional repo-relative or absolute path to <c>tenant-placement.v1.json</c>.
    /// When set, merges file contents over bound options.
    /// </summary>
    public string? ConfigPath { get; set; }

    public string Version { get; set; } = "1";

    public string DefaultTier { get; set; } = TenantPlacementTier.Shared;

    public Dictionary<string, TenantPlacementServiceOptions> Services { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public List<TenantPlacementEntryOptions> Placements { get; set; } = [];
}

public sealed class TenantPlacementServiceOptions
{
    public string DefaultConnectionName { get; set; } = string.Empty;
}

public sealed class TenantPlacementEntryOptions
{
    public string TenantKey { get; set; } = string.Empty;

    public string Tier { get; set; } = TenantPlacementTier.Shared;

    public string? DataRegion { get; set; }

    public bool Active { get; set; } = true;

    public string? Reason { get; set; }

    public Dictionary<string, TenantPlacementServiceBindingOptions> Services { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TenantPlacementServiceBindingOptions
{
    public string ConnectionName { get; set; } = string.Empty;
}

public static class TenantPlacementTier
{
    public const string Shared = "shared";
    public const string Dedicated = "dedicated";
}
