using System.Text;

namespace His.Hope.IdentityService.Application.InfraAudit;

/// <summary>
/// Builds LogQL server-side from validated parameters. Clients never send LogQL: sources are
/// an allowlist mapped to fixed log_type label values, and free text only ever becomes a
/// double-quoted line filter with escaping, so a parameter cannot change the query shape.
/// </summary>
public static class InfraAuditQuery
{
    public const string Job = "openship-access-audit";
    public const int MaxTextLength = 200;

    /// <summary>Public source name to the log_type label values it covers.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> SourceLogTypes =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["console"] = ["console_audit"],
            ["console-access"] = ["console_access_change", "console_access_check"],
            ["vault"] = ["vault_audit"],
            ["identity"] = ["identity_security"],
            ["headscale"] = ["headscale_event", "headplane_event", "headscale_backup"],
            // Shipped by later stages of the infra-access plan.
            ["kubernetes"] = ["kube_audit"],
            ["ssh"] = ["sshd_auth"],
            ["warpgate"] = ["warpgate_event"],
        };

    public static IReadOnlyCollection<string> Sources => SourceLogTypes.Keys.ToArray();

    /// <summary>Validates raw request parameters. Returns an error code or null.</summary>
    public static string? TryCreateFilter(
        DateTimeOffset now,
        InfraAuditOptions options,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? sources,
        string? actor,
        string? text,
        string? result,
        int? limit,
        out InfraAuditFilter filter)
    {
        filter = null!;
        var end = (to ?? now).ToUniversalTime();
        var start = (from ?? end.AddHours(-24)).ToUniversalTime();
        if (end > now.AddMinutes(5)) end = now;
        if (start >= end) return "invalid_range";
        if (end - start > TimeSpan.FromDays(options.MaxRangeDays)) return "range_too_long";
        if (start < now.AddDays(-options.RetentionDays)) return "beyond_retention";

        var sourceList = new List<string>();
        foreach (var source in (sources ?? string.Empty)
                     .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!SourceLogTypes.ContainsKey(source)) return "unknown_source";
            if (!sourceList.Contains(source)) sourceList.Add(source);
        }

        if (!IsSafeText(actor) || !IsSafeText(text)) return "invalid_text";
        if (result is not null && !InfraAuditResults.All.Contains(result)) return "unknown_result";

        var pageSize = limit ?? 100;
        if (pageSize < 1 || pageSize > options.MaxPageSize) return "invalid_limit";

        filter = new InfraAuditFilter(start, end, sourceList,
            string.IsNullOrWhiteSpace(actor) ? null : actor.Trim(),
            string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
            result, pageSize);
        return null;
    }

    public static string Build(InfraAuditFilter filter)
    {
        var logTypes = (filter.Sources.Count == 0 ? SourceLogTypes.Keys : filter.Sources)
            .SelectMany(source => SourceLogTypes[source])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        var query = new StringBuilder();
        // Label values come only from the allowlist above (no user input in the selector).
        query.Append("{job=\"").Append(Job).Append("\", log_type=~\"")
            .Append(string.Join('|', logTypes)).Append("\"}");
        if (filter.Actor is not null)
            query.Append(" |= ").Append(Quote(filter.Actor));
        if (filter.ActorAny is { Count: > 0 } actors)
            query.Append(" |~ ").Append(Quote("(?i)(" + string.Join('|', actors.Select(EscapeRegex)) + ")"));
        if (filter.Text is not null)
            query.Append(" |= ").Append(Quote(filter.Text));
        return query.ToString();
    }

    /// <summary>Double-quoted LogQL string literal with backslash and quote escaped.</summary>
    public static string Quote(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    /// <summary>Escapes RE2 metacharacters so a value matches literally.</summary>
    public static string EscapeRegex(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        foreach (var character in value)
        {
            if ("\\.+*?()|[]{}^$".Contains(character)) builder.Append('\\');
            builder.Append(character);
        }
        return builder.ToString();
    }

    private static bool IsSafeText(string? value) =>
        value is null ||
        (value.Length <= MaxTextLength && !value.Any(char.IsControl));
}
