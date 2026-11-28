using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using His.Hope.IdentityService.Application.InfraAudit;

namespace His.Hope.IdentityService.Application.HostSecurity;

/// <summary>
/// Maps one Loki line to a <see cref="HostSecurityEvent"/>:
/// <list type="bullet">
/// <item>tenant openship-access-audit, log_type host_integrity / sudo / sshd_auth (Vector JSON lines with security_event + severity);</item>
/// <item>tenant openship-runtime-security: falcosidekick text lines, rule/priority/host in the stream labels.</item>
/// </list>
/// Heartbeats (integrity_ok) and lines without a security event return null. Free text is masked.
/// </summary>
public static partial class HostSecurityNormalizer
{
    private const int MaxText = 600;

    public static HostSecurityEvent? Normalize(LokiLine raw)
    {
        if (raw.Labels.TryGetValue("rule", out var rule) && raw.Labels.ContainsKey("priority"))
            return Falco(raw, rule);

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(raw.Line);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
        if (root.ValueKind != JsonValueKind.Object) return null;
        var securityEvent = Str(root, "security_event");
        if (string.IsNullOrEmpty(securityEvent) || securityEvent == "integrity_ok") return null;
        return Str(root, "log_type") switch
        {
            "host_integrity" => Integrity(raw, root, securityEvent),
            "sudo" => Sudo(raw, root, securityEvent),
            "sshd_auth" => Login(raw, root, securityEvent),
            "ssh_gate" => Login(raw, root, securityEvent),
            "egress_baseline" => Egress(raw, root, securityEvent),
            _ => null,
        };
    }

    private static HostSecurityEvent Integrity(LokiLine raw, JsonElement root, string securityEvent)
    {
        var host = Str(root, "host") ?? Label(raw, "host") ?? "unknown";
        var fingerprint = Str(root, "fingerprint");
        var changes = new List<HostIntegrityChange>();
        if (root.TryGetProperty("changes", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                changes.Add(new HostIntegrityChange(
                    Str(item, "category") ?? "unknown",
                    Truncate(Str(item, "path"), 300) ?? "",
                    Str(item, "change") ?? "modified",
                    NormalizeSeverity(Str(item, "severity")),
                    Truncate(Str(item, "detail"), MaxText)));
            }
        }
        var count = Int(root, "drift_count") ?? changes.Count;
        var approvedBy = Str(root, "approved_by");
        var summary = securityEvent switch
        {
            "integrity_drift" => $"{count} unapproved change{(count == 1 ? "" : "s")}: " +
                string.Join(", ", changes.Take(3).Select(change => $"{change.Change} {change.Path}")) +
                (changes.Count > 3 ? ", ..." : ""),
            "integrity_baseline_initialized" => "Integrity baseline created (first run, or the baseline was deleted)",
            "integrity_baseline_approved" => $"Baseline approved by {approvedBy ?? "unknown"} ({count} change{(count == 1 ? "" : "s")}): {Str(root, "reason")}",
            _ => securityEvent,
        };
        var id = IdOf(raw);
        var ackKey = securityEvent == "integrity_drift" && !string.IsNullOrEmpty(fingerprint)
            ? $"drift:{host}:{fingerprint}"
            : $"evt:{id}";
        return new HostSecurityEvent(id, Fit(ackKey), raw.Timestamp, HostSecurityCategories.Integrity, securityEvent,
            NormalizeSeverity(Str(root, "severity")), host, approvedBy, Truncate(InfraAuditNormalizer.Mask(summary), MaxText)!,
            Truncate(Str(root, "message"), MaxText), fingerprint, changes, null);
    }

    private static HostSecurityEvent Sudo(LokiLine raw, JsonElement root, string securityEvent)
    {
        var user = Str(root, "user");
        var command = InfraAuditNormalizer.Mask(Str(root, "command"));
        var target = Str(root, "target_user") ?? "root";
        var failure = Str(root, "failure");
        var tool = securityEvent.StartsWith("su_", StringComparison.Ordinal) ? "su" : "sudo";
        var summary = string.IsNullOrEmpty(failure)
            ? $"{user} ran {tool} as {target}" + (string.IsNullOrEmpty(command) ? "" : $": {command}")
            : $"{user}: {tool} refused ({failure})" + (string.IsNullOrEmpty(command) ? "" : $": {command}");
        var tty = Str(root, "tty");
        var id = IdOf(raw);
        return new HostSecurityEvent(id, $"evt:{id}", raw.Timestamp, HostSecurityCategories.Sudo, securityEvent,
            NormalizeSeverity(Str(root, "severity")), Str(root, "host") ?? Label(raw, "host") ?? "unknown", user,
            Truncate(summary, MaxText)!, string.IsNullOrEmpty(tty) ? null : $"tty {tty}", null, [], null);
    }

    private static HostSecurityEvent Login(LokiLine raw, JsonElement root, string securityEvent)
    {
        var ip = Str(root, "source_ip");
        var summary = (Str(root, "action") ?? securityEvent) + (string.IsNullOrEmpty(ip) ? "" : $" from {ip}");
        var id = IdOf(raw);
        return new HostSecurityEvent(id, $"evt:{id}", raw.Timestamp, HostSecurityCategories.Login, securityEvent,
            NormalizeSeverity(Str(root, "severity")), Str(root, "host") ?? Label(raw, "host") ?? "unknown",
            Truncate(Str(root, "user"), 300), Truncate(InfraAuditNormalizer.Mask(summary), MaxText)!,
            Truncate(InfraAuditNormalizer.Mask(Str(root, "message")), MaxText), null, [], null);
    }

    private static HostSecurityEvent Egress(LokiLine raw, JsonElement root, string securityEvent)
    {
        string summary;
        string? detail = null;
        if (root.TryGetProperty("edge", out var edge) && edge.ValueKind == JsonValueKind.Object)
        {
            summary = $"New outbound connection {Str(edge, "source")} -> {Str(edge, "destination")} {Str(edge, "port")}";
            detail = $"{Str(edge, "kind")} edge, not seen while learning";
        }
        else
        {
            var total = Int(root, "edges_total") ?? 0;
            var added = Int(root, "new_edges") ?? 0;
            summary = $"{(Str(root, "mode") == "learning" ? "Learning" : "Checked")} outbound baseline: {total} known edges, {added} new this run";
            if (Str(root, "learn_until") is { } until) detail = $"learning until {until}";
        }
        var id = IdOf(raw);
        return new HostSecurityEvent(id, $"evt:{id}", raw.Timestamp, HostSecurityCategories.Network, securityEvent,
            NormalizeSeverity(Str(root, "severity")), Str(root, "host") ?? Label(raw, "host") ?? "unknown", null,
            Truncate(summary, MaxText)!, detail, null, [], null);
    }

    private static HostSecurityEvent Falco(LokiLine raw, string rule)
    {
        var priority = Label(raw, "priority") ?? "Notice";
        // "08:41:49.145969500: Notice Unexpected connection ... | connection=... user=..."
        var text = FalcoPrefix().Replace(raw.Line, "");
        var bar = text.IndexOf(" | ", StringComparison.Ordinal);
        var output = bar >= 0 ? text[..bar] : text;
        var fields = bar >= 0 ? text[(bar + 3)..] : null;
        if (output.StartsWith(priority + " ", StringComparison.OrdinalIgnoreCase)) output = output[(priority.Length + 1)..];
        var ns = Label(raw, "k8s_ns_name");
        var pod = Label(raw, "k8s_pod_name");
        // Host processes (no pod) are labelled "host" by the Vector agent.
        if (ns is "host" or "<NA>") ns = null;
        if (pod is "host" or "<NA>") pod = null;
        var actor = ns is null && pod is null ? null : $"{ns}/{pod}";
        var id = IdOf(raw);
        return new HostSecurityEvent(id, $"evt:{id}", raw.Timestamp, HostSecurityCategories.Runtime, rule,
            FalcoSeverity(priority), Label(raw, "hostname") ?? "unknown", actor,
            Truncate(InfraAuditNormalizer.Mask(output.Trim()), MaxText)!,
            Truncate(InfraAuditNormalizer.Mask(fields), MaxText), null, [], null);
    }

    public static string FalcoSeverity(string priority) => priority.ToLowerInvariant() switch
    {
        "emergency" or "alert" or "critical" => HostSecuritySeverities.Critical,
        "error" or "warning" => HostSecuritySeverities.Warning,
        _ => HostSecuritySeverities.Info,
    };

    private static string NormalizeSeverity(string? value) => value switch
    {
        HostSecuritySeverities.Critical => HostSecuritySeverities.Critical,
        HostSecuritySeverities.Warning => HostSecuritySeverities.Warning,
        _ => HostSecuritySeverities.Info,
    };

    private static string IdOf(LokiLine raw)
    {
        var ticks = raw.Timestamp.UtcTicks.ToString(CultureInfo.InvariantCulture);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(ticks + "\n" + raw.Line));
        return Convert.ToHexString(hash.AsSpan(0, 10)).ToLowerInvariant();
    }

    private static string Fit(string value) => value.Length <= 100 ? value : value[..100];

    private static string? Label(LokiLine raw, string name) =>
        raw.Labels.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    private static string? Str(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? Int(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;

    private static string? Truncate(string? value, int max) =>
        value is { Length: var length } && length > max ? value[..max] + "…" : value;

    // "08:41:49.145969500: " or ISO "2026-11-27T09:16:26.099078464+0000: " (time_format_iso_8601).
    [GeneratedRegex(@"^(?:\d{4}-\d{2}-\d{2}T)?\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{4}|[+-]\d{2}:\d{2})?:\s*")]
    private static partial Regex FalcoPrefix();
}
