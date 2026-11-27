namespace His.Hope.IdentityService.Application.HostSecurity;

/// <summary>
/// Host and workload security events (OpenShip docs/security/host-security-monitoring.md):
/// config integrity drift, sudo/su, console logins (tenant openship-access-audit) and Falco
/// runtime alerts (tenant openship-runtime-security). Read live from Loki through the query
/// relay; only acknowledgements are stored here (as audit_logs rows, hash-chained).
/// </summary>
public static class HostSecuritySeverities
{
    public const string Critical = "critical";
    public const string Warning = "warning";
    public const string Info = "info";

    public static readonly IReadOnlyList<string> All = [Critical, Warning, Info];

    public static int Rank(string? severity) => severity switch
    {
        Critical => 2,
        Warning => 1,
        _ => 0,
    };
}

public static class HostSecurityCategories
{
    public const string Integrity = "integrity";
    public const string Sudo = "sudo";
    public const string Login = "login";
    public const string Runtime = "runtime";

    public static readonly IReadOnlyList<string> All = [Integrity, Sudo, Login, Runtime];
}

/// <summary>One changed item of an integrity drift.</summary>
public sealed record HostIntegrityChange(string Category, string Path, string Change, string Severity, string? Detail);

/// <summary>
/// One security event. <see cref="AckKey"/> is what an acknowledgement covers: the drift
/// fingerprint for integrity drift (every 5-minute repeat of the same drift shares it), the
/// event itself otherwise.
/// </summary>
public sealed record HostSecurityEvent(
    string Id,
    string AckKey,
    DateTimeOffset Timestamp,
    string Category,
    string SecurityEvent,
    string Severity,
    string Host,
    string? Actor,
    string Summary,
    string? Detail,
    string? Fingerprint,
    IReadOnlyList<HostIntegrityChange> Changes,
    HostSecurityAck? Ack);

public sealed record HostSecurityAck(string Key, string AckedBy, DateTimeOffset AckedAt, string? Note);

/// <summary>Integrity check state of one host, from its latest report.</summary>
public sealed record HostIntegrityStatus(
    string Host,
    DateTimeOffset LastReportAt,
    string LastEvent,
    string Severity,
    int DriftCount,
    string? Fingerprint,
    bool Stale);

public sealed record HostSecurityFilter(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<string> Categories,
    string MinSeverity,
    string? Host,
    string? Text,
    bool UnacknowledgedOnly,
    int Limit);

public sealed record HostSecurityPage(
    IReadOnlyList<HostSecurityEvent> Items,
    IReadOnlyDictionary<string, int> UnacknowledgedBySeverity,
    bool Truncated,
    IReadOnlyList<string> UnavailableSources);

/// <summary>One raw Loki line with its stream labels.</summary>
public sealed record LokiLine(DateTimeOffset Timestamp, string Line, IReadOnlyDictionary<string, string> Labels);

/// <summary>Loki query_range over a chosen tenant (implemented over the query relay).</summary>
public interface ILokiStreamReader
{
    bool IsConfigured { get; }

    /// <summary>Newest first; at most <paramref name="limit"/> lines within [from, to].</summary>
    Task<IReadOnlyList<LokiLine>> ReadStreamsAsync(
        string tenant, string logQl, DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken cancellationToken);
}

/// <summary>Acknowledgements of security events (stored as audit_logs rows).</summary>
public interface IHostSecurityAckStore
{
    Task<IReadOnlyDictionary<string, HostSecurityAck>> GetAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken);
}

/// <summary>The Loki relay could not answer (not configured, unreachable, HTTP error).</summary>
public class LokiUnavailableException(string reason) : Exception(reason)
{
    public string Reason { get; } = reason;
}
