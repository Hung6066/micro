using System.Text.RegularExpressions;
using His.Hope.IdentityService.Application.InfraAudit;
using Microsoft.Extensions.Options;

namespace His.Hope.IdentityService.Application.HostSecurity;

/// <summary>
/// Security page queries: merges the access-audit tenant (integrity, sudo, logins) with the
/// runtime-security tenant (Falco), attaches acknowledgements, and reports per-host
/// integrity check state. One source failing still returns the other (UnavailableSources).
/// </summary>
public sealed partial class HostSecurityService(
    ILokiStreamReader reader,
    IHostSecurityAckStore acks,
    IOptions<InfraAuditOptions> options,
    TimeProvider clock)
{
    public const int MaxLimit = 500;
    public const int MaxRangeDays = 31;
    /// <summary>A host whose integrity check has not reported for this long is stale (the timer runs every 5 min).</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan StatusWindow = TimeSpan.FromHours(6);

    private readonly InfraAuditOptions _options = options.Value;

    public bool IsConfigured => reader.IsConfigured;

    /// <summary>Validates raw query parameters; returns an error code or null.</summary>
    public string? TryCreateFilter(
        DateTimeOffset? from, DateTimeOffset? to, string? categories, string? minSeverity, string? host,
        string? text, bool? unacknowledgedOnly, int? limit, out HostSecurityFilter filter)
    {
        var now = clock.GetUtcNow();
        var end = to ?? now;
        var start = from ?? end.AddHours(-24);
        filter = null!;
        if (end > now.AddMinutes(5)) end = now;
        if (start >= end) return "from_after_to";
        if (end - start > TimeSpan.FromDays(MaxRangeDays)) return "range_too_long";
        if (start < now.AddDays(-_options.RetentionDays)) return "before_retention";
        var cats = string.IsNullOrWhiteSpace(categories)
            ? HostSecurityCategories.All
            : categories.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
        if (cats.Any(cat => !HostSecurityCategories.All.Contains(cat))) return "unknown_category";
        var severity = string.IsNullOrWhiteSpace(minSeverity) ? HostSecuritySeverities.Info : minSeverity.Trim();
        if (!HostSecuritySeverities.All.Contains(severity)) return "unknown_severity";
        if (host is { Length: > 0 } && !HostName().IsMatch(host)) return "invalid_host";
        if (text is { Length: > 200 }) return "text_too_long";
        var size = limit ?? 200;
        if (size is < 1 or > MaxLimit) return "invalid_limit";
        filter = new HostSecurityFilter(start, end, cats, severity, string.IsNullOrWhiteSpace(host) ? null : host.Trim(),
            string.IsNullOrWhiteSpace(text) ? null : text.Trim(), unacknowledgedOnly ?? false, size);
        return null;
    }

    public async Task<HostSecurityPage> GetPageAsync(HostSecurityFilter filter, CancellationToken cancellationToken)
    {
        var raw = new List<LokiLine>();
        var unavailable = new List<string>();
        var truncated = false;
        foreach (var (source, tenant, query) in Queries(filter))
        {
            try
            {
                var lines = await reader.ReadStreamsAsync(tenant, query, filter.From, filter.To, filter.Limit, cancellationToken);
                truncated |= lines.Count >= filter.Limit;
                raw.AddRange(lines);
            }
            catch (LokiUnavailableException)
            {
                unavailable.Add(source);
            }
        }
        if (raw.Count == 0 && unavailable.Count > 0 && unavailable.Count == Queries(filter).Count())
            throw new LokiUnavailableException("all_sources_unavailable");

        var minRank = HostSecuritySeverities.Rank(filter.MinSeverity);
        var events = raw
            .Select(HostSecurityNormalizer.Normalize)
            .OfType<HostSecurityEvent>()
            .Where(item => filter.Categories.Contains(item.Category))
            .Where(item => HostSecuritySeverities.Rank(item.Severity) >= minRank)
            .Where(item => filter.Host is null || string.Equals(item.Host, filter.Host, StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.Id).Select(group => group.First()) // .19 sshd is shipped twice (DaemonSet + host shipper)
            .OrderByDescending(item => item.Timestamp)
            .ToList();

        var found = await acks.GetAsync(events.Select(item => item.AckKey).Distinct().ToList(), cancellationToken);
        events = events.Select(item => found.TryGetValue(item.AckKey, out var ack) ? item with { Ack = ack } : item).ToList();
        var open = events.Where(item => item.Ack is null && item.Severity != HostSecuritySeverities.Info)
            .GroupBy(item => item.Severity).ToDictionary(group => group.Key, group => group.Select(item => item.AckKey).Distinct().Count());
        if (filter.UnacknowledgedOnly) events = events.Where(item => item.Ack is null).ToList();
        if (events.Count > filter.Limit) events = events.GetRange(0, filter.Limit);
        return new HostSecurityPage(events, open, truncated, unavailable);
    }

    /// <summary>
    /// The newest drift event of <paramref name="host"/> with exactly <paramref name="fingerprint"/>
    /// in the last 24 hours (the server re-reads severity and changes; it never trusts the browser).
    /// </summary>
    public async Task<HostSecurityEvent?> FindDriftAsync(string host, string fingerprint, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var lines = await reader.ReadStreamsAsync(_options.Tenant,
            $"{{job=\"openship-access-audit\", log_type=\"host_integrity\"}} |= \"{fingerprint}\"", now.AddHours(-24), now, 50, cancellationToken);
        return lines
            .Select(HostSecurityNormalizer.Normalize)
            .OfType<HostSecurityEvent>()
            .Where(item => item.SecurityEvent == "integrity_drift" && item.Fingerprint == fingerprint
                           && string.Equals(item.Host, host, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Timestamp)
            .FirstOrDefault();
    }

    /// <summary>Latest integrity report per host seen in the last hours.</summary>
    public async Task<IReadOnlyList<HostIntegrityStatus>> GetHostStatusAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var lines = await reader.ReadStreamsAsync(_options.Tenant,
            "{job=\"openship-access-audit\", log_type=\"host_integrity\"}", now - StatusWindow, now, 2000, cancellationToken);
        var latest = new Dictionary<string, (DateTimeOffset At, string Event, string Severity, int Count, string? Fingerprint)>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.OrderByDescending(item => item.Timestamp))
        {
            if (!TryReadIntegrity(line.Line, out var host, out var securityEvent, out var severity, out var count, out var fingerprint)) continue;
            if (!latest.ContainsKey(host)) latest[host] = (line.Timestamp, securityEvent, severity, count, fingerprint);
        }
        return latest
            .Select(pair => new HostIntegrityStatus(pair.Key, pair.Value.At, pair.Value.Event, pair.Value.Severity,
                pair.Value.Count, pair.Value.Fingerprint, now - pair.Value.At > StaleAfter))
            .OrderBy(item => item.Host, StringComparer.Ordinal)
            .ToList();
    }

    private IEnumerable<(string Source, string Tenant, string Query)> Queries(HostSecurityFilter filter)
    {
        var types = new List<string>();
        if (filter.Categories.Contains(HostSecurityCategories.Integrity)) types.Add("host_integrity");
        if (filter.Categories.Contains(HostSecurityCategories.Sudo)) types.Add("sudo");
        if (filter.Categories.Contains(HostSecurityCategories.Login)) { types.Add("sshd_auth"); types.Add("ssh_gate"); }
        if (filter.Categories.Contains(HostSecurityCategories.Network)) types.Add("egress_baseline");
        var text = filter.Text is null ? "" : $" |~ \"(?i){EscapeLogQl(EscapeRe2(filter.Text))}\"";
        if (types.Count > 0)
        {
            var severities = SeverityRegex(filter.MinSeverity);
            yield return ("access-audit", _options.Tenant,
                $"{{job=\"openship-access-audit\", log_type=~\"{string.Join('|', types)}\"}} |= \"security_event\"{text}" +
                $" | json security_event=\"security_event\", sev=\"severity\" | security_event!=\"\" | security_event!=\"integrity_ok\" | sev=~\"{severities}\"");
        }
        if (filter.Categories.Contains(HostSecurityCategories.Runtime))
        {
            var priorities = filter.MinSeverity switch
            {
                HostSecuritySeverities.Critical => "Critical|Alert|Emergency",
                HostSecuritySeverities.Warning => "Warning|Error|Critical|Alert|Emergency",
                _ => "Notice|Informational|Warning|Error|Critical|Alert|Emergency",
            };
            yield return ("runtime-security", _options.RuntimeSecurityTenant,
                $"{{source=~\".+\", priority=~\"{priorities}\"}}{text}");
        }
    }

    private static string SeverityRegex(string minSeverity) => minSeverity switch
    {
        HostSecuritySeverities.Critical => "critical",
        HostSecuritySeverities.Warning => "warning|critical",
        _ => "info|warning|critical|",
    };

    /// <summary>Literal text inside an RE2 pattern: only metacharacters are escaped (RE2 rejects "\ ").</summary>
    internal static string EscapeRe2(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length * 2);
        foreach (var ch in value)
        {
            if (char.IsControl(ch)) { builder.Append(' '); continue; }
            if (@"\.+*?()|[]{}^$".Contains(ch)) builder.Append('\\');
            builder.Append(ch);
        }
        return builder.ToString();
    }

    private static string EscapeLogQl(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static bool TryReadIntegrity(string line, out string host, out string securityEvent, out string severity, out int count, out string? fingerprint)
    {
        host = securityEvent = severity = "";
        count = 0;
        fingerprint = null;
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object) return false;
            host = root.TryGetProperty("host", out var h) ? h.GetString() ?? "" : "";
            securityEvent = root.TryGetProperty("security_event", out var e) ? e.GetString() ?? "" : "";
            severity = root.TryGetProperty("severity", out var s) ? s.GetString() ?? "info" : "info";
            count = root.TryGetProperty("drift_count", out var c) && c.TryGetInt32(out var n) ? n : 0;
            fingerprint = root.TryGetProperty("fingerprint", out var f) ? f.GetString() : null;
            return host.Length > 0 && securityEvent.Length > 0;
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9.-]{0,62}$")]
    private static partial Regex HostName();
}
