namespace His.Hope.IdentityService.Application.InfraAudit;

/// <summary>
/// Infrastructure access audit (OpenShip consoles, Vault, IdentityService security events,
/// Headscale, and later kube-apiserver / sshd / Warpgate). Events are read from the Loki
/// tenant <see cref="Tenant"/> through the OpenShip Loki query relay; nothing is stored here.
/// </summary>
public sealed class InfraAuditOptions
{
    public const string SectionName = "InfraAudit";

    /// <summary>Full query_range URL of the relay, e.g. https://openship-loki-query-relay.openship-logging.svc.cluster.local:8443/loki/api/v1/query_range.</summary>
    public string? QueryRangeUrl { get; set; }

    public string Tenant { get; set; } = "openship-access-audit";

    /// <summary>Falco runtime alerts (falcosidekick), read by the Security page.</summary>
    public string RuntimeSecurityTenant { get; set; } = "openship-runtime-security";

    /// <summary>Relay basic-auth user and password (secret; never logged).</summary>
    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary>PEM CA that signed the relay's serving certificate; system roots when unset.</summary>
    public string? CaCertificatePath { get; set; }

    /// <summary>
    /// Same CA inline (public certificate, not a secret), for hosts that set configuration
    /// through environment variables only. Used when <see cref="CaCertificatePath"/> is unset.
    /// </summary>
    public string? CaCertificatePem { get; set; }

    /// <summary>Loki keeps this tenant for 90 days; queries may not reach further back.</summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>Longest window of one query.</summary>
    public int MaxRangeDays { get; set; } = 31;

    public int MaxPageSize { get; set; } = 500;

    public int MaxExportRows { get; set; } = 10_000;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    public bool IsConfigured =>
        Uri.TryCreate(QueryRangeUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password) &&
        !string.IsNullOrWhiteSpace(Tenant);
}

/// <summary>One access event in the common schema, whatever its source.</summary>
public sealed record InfraAuditEvent(
    DateTimeOffset Timestamp,
    string Source,
    string? Actor,
    string Action,
    string? Target,
    string Result,
    string? SourceIp,
    string? CorrelationId,
    string? Detail,
    string LogType);

public static class InfraAuditResults
{
    public const string Allowed = "allowed";
    public const string Denied = "denied";
    public const string Error = "error";
    public const string Info = "info";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.Ordinal) { Allowed, Denied, Error, Info };
}

/// <summary>Validated filter for one page of events.</summary>
public sealed record InfraAuditFilter(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<string> Sources,
    string? Actor,
    string? Text,
    string? Result,
    int Limit,
    IReadOnlyList<string>? ActorAny = null);

public sealed record InfraAuditPage(
    IReadOnlyList<InfraAuditEvent> Items,
    DateTimeOffset? NextTo);

public sealed record InfraAuditSummary(
    DateTimeOffset From,
    DateTimeOffset To,
    int Total,
    bool Truncated,
    IReadOnlyDictionary<string, int> BySource,
    IReadOnlyDictionary<string, int> ByResult,
    IReadOnlyList<KeyValuePair<string, int>> TopActors,
    IReadOnlyList<KeyValuePair<string, int>> TopTargets,
    IReadOnlyList<KeyValuePair<DateTimeOffset, int>> PerHour);

/// <summary>Reads raw audit lines; implemented over the Loki query relay.</summary>
public interface IInfraAuditLogReader
{
    bool IsConfigured { get; }

    /// <summary>Newest first; at most <paramref name="limit"/> raw lines within [from, to].</summary>
    Task<IReadOnlyList<(DateTimeOffset Timestamp, string Line)>> ReadAsync(
        string logQl, DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken cancellationToken);
}
