namespace His.Hope.IdentityService.Application.OpenShip;

/// <summary>
/// Configuration for OpenShip tailnet console access. IdentityService is the
/// source of truth: IAM groups whose key starts with <see cref="GroupPrefix"/>
/// are the console roles, and an approved break-glass request for
/// <see cref="WritePermission"/> is the only way to obtain time-boxed write.
/// </summary>
public sealed class OpenShipConsoleOptions
{
    public const string SectionName = "OpenShip:Console";

    public static readonly string[] DefaultClients =
        ["openship-headscale", "openship-grafana", "openship-vault", "openship-headplane"];

    public const string DefaultGroupPrefix = "openship-console-";
    public const string DefaultWritePermission = "openship.console.write";

    /// <summary>
    /// OIDC client ids that receive the <c>groups</c> claim. Other clients never
    /// see console group membership. Left empty here because the configuration
    /// binder appends to (rather than replaces) pre-populated arrays; defaults are
    /// applied in post-configuration when nothing is configured.
    /// </summary>
    public string[] Clients { get; set; } = [];

    public string GroupPrefix { get; set; } = DefaultGroupPrefix;

    public string WritePermission { get; set; } = DefaultWritePermission;

    /// <summary>Fills unset values with the documented defaults.</summary>
    public void ApplyDefaults()
    {
        Clients = (Clients ?? [])
            .Where(client => !string.IsNullOrWhiteSpace(client))
            .Select(client => client.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (Clients.Length == 0)
            Clients = [.. DefaultClients];
        if (string.IsNullOrWhiteSpace(GroupPrefix))
            GroupPrefix = DefaultGroupPrefix;
        if (string.IsNullOrWhiteSpace(WritePermission))
            WritePermission = DefaultWritePermission;
    }

    public bool IsConsoleClient(string? clientId) =>
        !string.IsNullOrWhiteSpace(clientId) && Clients.Contains(clientId, StringComparer.Ordinal);
}
