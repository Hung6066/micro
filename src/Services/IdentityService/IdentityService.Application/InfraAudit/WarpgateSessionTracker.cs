using System.Text.RegularExpressions;

namespace His.Hope.IdentityService.Application.InfraAudit;

/// <summary>A Warpgate session that logged a start and no end in the scanned window.</summary>
public sealed record WarpgateOpenSession(
    string SessionId,
    string? User,
    string? Target,
    DateTimeOffset StartedAt,
    string? SourceIp);

public sealed record WarpgateOpenSessions(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<WarpgateOpenSession> Items,
    bool Truncated);

/// <summary>
/// Derives open Warpgate sessions from <c>warpgate_event</c> lines. The access-audit Vector
/// shipper puts the Warpgate session UUID in <c>correlation_id</c> and the audit message in
/// <c>action</c> ("... started", "... ended/closed"; shell commands are "Shell command: ...").
/// A session is open when the window holds a start for its id and no end.
/// </summary>
public static partial class WarpgateSessionTracker
{
    public const string Source = "warpgate";
    public const string LogType = "warpgate_event";
    public const int DefaultHours = 24;

    /// <summary>Fixed line filter: only lifecycle lines, so shell commands do not crowd the scan.</summary>
    public const string LifecyclePattern = "(?i)(started|ended|closed|finished|terminated|disconnected)";

    private const string ShellCommandPrefix = "Shell command:";

    /// <summary>Validates the look-back window. Returns an error code or null.</summary>
    public static string? TryCreateWindow(
        DateTimeOffset now, InfraAuditOptions options, int? hours, out DateTimeOffset from, out DateTimeOffset to)
    {
        to = now.ToUniversalTime();
        from = to;
        var span = hours ?? DefaultHours;
        if (span < 1) return "invalid_range";
        if (span > options.MaxRangeDays * 24) return "range_too_long";
        from = to.AddHours(-span);
        return null;
    }

    /// <summary>
    /// LogQL over the allowlisted warpgate selector plus a constant lifecycle filter. No request
    /// value reaches the query: the window is passed to Loki as start/end, not in the text.
    /// </summary>
    public static string BuildQuery(DateTimeOffset from, DateTimeOffset to, int limit) =>
        InfraAuditQuery.Build(new InfraAuditFilter(from, to, [Source], null, null, null, limit)) +
        " |~ " + InfraAuditQuery.Quote(LifecyclePattern);

    public static bool IsStart(InfraAuditEvent item) =>
        !IsShellCommand(item) && !IsEnd(item) && StartWord().IsMatch(item.Action);

    public static bool IsEnd(InfraAuditEvent item) =>
        !IsShellCommand(item) && EndWord().IsMatch(item.Action);

    /// <summary>Pairs start/end by session id; newest open session first.</summary>
    public static IReadOnlyList<WarpgateOpenSession> OpenSessions(IEnumerable<InfraAuditEvent> events) =>
        events
            .Where(item => item.LogType == LogType && !string.IsNullOrWhiteSpace(item.CorrelationId))
            .GroupBy(item => item.CorrelationId!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(session => !session.Any(IsEnd))
            .Select(session =>
            {
                var start = session.Where(IsStart).OrderBy(item => item.Timestamp).FirstOrDefault();
                if (start is null) return null;
                // An end-less session's other lines (shell commands) may carry what the start lacks.
                string? First(Func<InfraAuditEvent, string?> pick) =>
                    new[] { start }.Concat(session.OrderBy(item => item.Timestamp))
                        .Select(pick).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                return new WarpgateOpenSession(session.Key, First(item => item.Actor), First(item => item.Target),
                    start.Timestamp, First(item => item.SourceIp));
            })
            .OfType<WarpgateOpenSession>()
            .OrderByDescending(session => session.StartedAt)
            .ToList();

    private static bool IsShellCommand(InfraAuditEvent item) =>
        item.Action.StartsWith(ShellCommandPrefix, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"(?i)\bstarted\b")]
    private static partial Regex StartWord();

    [GeneratedRegex(@"(?i)\b(ended|closed|finished|terminated|disconnected)\b")]
    private static partial Regex EndWord();
}
