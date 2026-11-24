namespace His.Hope.IdentityService.Application.OpenShip;

/// <summary>
/// Configuration for OpenShip tailnet console and infrastructure access.
/// IdentityService is the source of truth: IAM groups whose key starts with
/// <see cref="GroupPrefix"/> (or one of <see cref="ExtraGroupPrefixes"/>) are
/// the access roles, and an approved break-glass request is the only way to
/// obtain time-boxed elevation (console write, node root, cluster admin).
/// </summary>
public sealed class OpenShipConsoleOptions
{
    public const string SectionName = "OpenShip:Console";

    public static readonly string[] DefaultClients =
    [
        "openship-headscale", "openship-grafana", "openship-vault", "openship-headplane", "openship-platform",
        // Infrastructure access (Warpgate bastion and kubectl via kubelogin).
        "openship-warpgate", "openship-kubectl"
    ];

    public const string DefaultGroupPrefix = "openship-console-";
    public const string DefaultWritePermission = "openship.console.write";

    public static readonly string[] DefaultExtraGroupPrefixes = ["openship-ops-"];

    public static readonly OpenShipBreakGlassGroup[] DefaultBreakGlassGroups =
    [
        new() { Permission = "openship.node.root", Group = "openship-breakglass-node-admin" },
        new() { Permission = "openship.cluster.admin", Group = "openship-breakglass-cluster-admin" }
    ];

    public static readonly string[] DefaultShortLivedClients = ["openship-kubectl"];

    public static readonly TimeSpan DefaultShortLivedAccessTokenLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// OIDC client ids that receive the <c>groups</c> claim. Other clients never
    /// see console group membership. Left empty here because the configuration
    /// binder appends to (rather than replaces) pre-populated arrays; defaults are
    /// applied in post-configuration when nothing is configured.
    /// </summary>
    public string[] Clients { get; set; } = [];

    public string GroupPrefix { get; set; } = DefaultGroupPrefix;

    /// <summary>
    /// Further IAM group key prefixes issued in the <c>groups</c> claim, such as
    /// <c>openship-ops-</c> for SSH / Kubernetes roles. Defaults when unset.
    /// </summary>
    public string[] ExtraGroupPrefixes { get; set; } = [];

    public string WritePermission { get; set; } = DefaultWritePermission;

    /// <summary>
    /// Break-glass permissions that, while an approved and unexpired grant
    /// exists, add a dynamic group to the <c>groups</c> claim (for example
    /// <c>openship-breakglass-node-admin</c>). Tokens carrying such a group
    /// never outlive the grant. Defaults when unset.
    /// </summary>
    public OpenShipBreakGlassGroup[] BreakGlassGroups { get; set; } = [];

    /// <summary>
    /// Clients whose access tokens are capped at
    /// <see cref="ShortLivedAccessTokenLifetime"/> so that removing a member or
    /// disabling a user takes effect within minutes (kubectl). Defaults when unset.
    /// </summary>
    public string[] ShortLivedClients { get; set; } = [];

    public TimeSpan ShortLivedAccessTokenLifetime { get; set; } = DefaultShortLivedAccessTokenLifetime;

    /// <summary>Fills unset values with the documented defaults.</summary>
    public void ApplyDefaults()
    {
        Clients = Normalize(Clients);
        if (Clients.Length == 0)
            Clients = [.. DefaultClients];
        if (string.IsNullOrWhiteSpace(GroupPrefix))
            GroupPrefix = DefaultGroupPrefix;
        ExtraGroupPrefixes = Normalize(ExtraGroupPrefixes);
        if (ExtraGroupPrefixes.Length == 0)
            ExtraGroupPrefixes = [.. DefaultExtraGroupPrefixes];
        if (string.IsNullOrWhiteSpace(WritePermission))
            WritePermission = DefaultWritePermission;
        BreakGlassGroups = (BreakGlassGroups ?? [])
            .Where(item => item is not null &&
                !string.IsNullOrWhiteSpace(item.Permission) && !string.IsNullOrWhiteSpace(item.Group))
            .Select(item => new OpenShipBreakGlassGroup { Permission = item.Permission.Trim(), Group = item.Group.Trim() })
            .DistinctBy(item => item.Permission, StringComparer.Ordinal)
            .ToArray();
        if (BreakGlassGroups.Length == 0)
            BreakGlassGroups = DefaultBreakGlassGroups
                .Select(item => new OpenShipBreakGlassGroup { Permission = item.Permission, Group = item.Group })
                .ToArray();
        ShortLivedClients = Normalize(ShortLivedClients);
        if (ShortLivedClients.Length == 0)
            ShortLivedClients = [.. DefaultShortLivedClients];
        if (ShortLivedAccessTokenLifetime <= TimeSpan.Zero)
            ShortLivedAccessTokenLifetime = DefaultShortLivedAccessTokenLifetime;
    }

    public bool IsConsoleClient(string? clientId) =>
        !string.IsNullOrWhiteSpace(clientId) && Clients.Contains(clientId, StringComparer.Ordinal);

    public bool IsShortLivedClient(string? clientId) =>
        !string.IsNullOrWhiteSpace(clientId) && ShortLivedClients.Contains(clientId, StringComparer.Ordinal);

    /// <summary>
    /// True when a static IAM group key is issued in the <c>groups</c> claim.
    /// A static group can never carry a break-glass group name: those are only
    /// ever added for an approved, unexpired grant, so creating an IAM group
    /// with the same key must not bypass the four-eyes approval.
    /// </summary>
    public bool IsIssuedGroup(string? key) =>
        !string.IsNullOrWhiteSpace(key) &&
        !BreakGlassGroups.Any(item => string.Equals(item.Group, key, StringComparison.OrdinalIgnoreCase)) &&
        (key.StartsWith(GroupPrefix, StringComparison.Ordinal) ||
         ExtraGroupPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal)));

    private static string[] Normalize(string[]? values) =>
        (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}

/// <summary>Maps a break-glass permission to the dynamic group it grants while active.</summary>
public sealed class OpenShipBreakGlassGroup
{
    public string Permission { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;
}
