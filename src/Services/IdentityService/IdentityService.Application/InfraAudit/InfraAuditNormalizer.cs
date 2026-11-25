using System.Text.Json;
using System.Text.RegularExpressions;

namespace His.Hope.IdentityService.Application.InfraAudit;

/// <summary>
/// Maps one Vector-shaped audit line (tenant openship-access-audit) to <see cref="InfraAuditEvent"/>.
/// Unknown log types still produce an event so new sources show up before they get a mapping.
/// Free-form text is masked: URL credentials, password/secret/token assignments and bearer tokens.
/// </summary>
public static partial class InfraAuditNormalizer
{
    public static InfraAuditEvent? Normalize(DateTimeOffset timestamp, string line)
    {
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(line);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
        if (root.ValueKind != JsonValueKind.Object) return null;

        var logType = Str(root, "log_type") ?? "unknown";
        return logType switch
        {
            "console_audit" => Console(timestamp, root, logType),
            "console_access_change" or "console_access_check" => ConsoleAccess(timestamp, root, logType),
            "vault_audit" => Vault(timestamp, root, logType),
            "identity_security" => Identity(timestamp, root, logType),
            "headscale_backup" => new InfraAuditEvent(timestamp, "headscale", null, "backup",
                Str(root, "target"), ResultOf(Str(root, "result")), null, null, Mask(Str(root, "detail")), logType),
            "headscale_event" or "headplane_event" => new InfraAuditEvent(timestamp, "headscale", null,
                Truncate(Mask(Str(root, "event")) ?? "event", 160), Str(root, "path"),
                ResultFromStatus(Int(root, "status"), Str(root, "level")), Str(root, "remote"), null, null, logType),
            _ => Generic(timestamp, root, logType),
        };
    }

    private static InfraAuditEvent Console(DateTimeOffset ts, JsonElement r, string logType)
    {
        var console = Str(r, "console") ?? "console";
        var write = Str(r, "write") == "1" ? " (write)" : string.Empty;
        return new InfraAuditEvent(ts, "console", Blank(Str(r, "user")),
            $"{Str(r, "method") ?? "GET"} {console}{write}",
            $"{console}:{Str(r, "uri")}",
            ResultFromStatus(Int(r, "status"), null),
            Str(r, "remote_addr"), Str(r, "request_id"),
            $"status={Int(r, "status")} time={Str(r, "request_time")}s", logType);
    }

    private static InfraAuditEvent ConsoleAccess(DateTimeOffset ts, JsonElement r, string logType)
    {
        var isChange = logType == "console_access_change";
        return new InfraAuditEvent(ts, "console-access", Blank(Str(r, "user")),
            isChange ? $"access {Str(r, "action") ?? "change"}" : "access check",
            Str(r, "console") ?? Str(r, "role"),
            isChange ? InfraAuditResults.Info : ResultOf(Str(r, "result")),
            null, null, Mask(Str(r, "note") ?? Str(r, "error")), logType);
    }

    private static InfraAuditEvent Vault(DateTimeOffset ts, JsonElement r, string logType)
    {
        var path = Str(r, "path");
        var error = Str(r, "error");
        var actor = Str(r, "display_name");
        // Vault prefixes OIDC aliases with the mount ("oidc-user@example.com").
        if (actor is not null && actor.StartsWith("oidc-", StringComparison.Ordinal)) actor = actor[5..];
        return new InfraAuditEvent(ts, "vault", Blank(actor),
            $"{Str(r, "operation") ?? "request"} {path}",
            path,
            error is null ? InfraAuditResults.Allowed :
                error.Contains("permission denied", StringComparison.OrdinalIgnoreCase) ? InfraAuditResults.Denied : InfraAuditResults.Error,
            Str(r, "remote_address"), null, Mask(error), logType);
    }

    private static InfraAuditEvent Identity(DateTimeOffset ts, JsonElement r, string logType)
    {
        var template = Str(r, "template") ?? "identity event";
        var status = Str(r, "status");
        var security = Str(r, "security_event");
        var result = security is "identity_auth_failure" or "identity_lockout" or "identity_authz_denied"
            ? InfraAuditResults.Denied
            : string.Equals(status, "Allow", StringComparison.OrdinalIgnoreCase) ? InfraAuditResults.Allowed
            : string.Equals(Str(r, "level"), "Error", StringComparison.OrdinalIgnoreCase) ? InfraAuditResults.Error
            : InfraAuditResults.Info;
        return new InfraAuditEvent(ts, "identity", Blank(Str(r, "user_id")),
            Truncate(Str(r, "action") is { Length: > 0 } action ? $"{action} {Str(r, "path") ?? string.Empty}".Trim() : template, 160),
            Str(r, "client_id") ?? Str(r, "request_path") ?? Str(r, "path"),
            result, Str(r, "client_ip"), Str(r, "correlation_id"),
            Mask(string.Join(' ', new[] { Str(r, "reason"), security }.Where(value => !string.IsNullOrEmpty(value)))) is { Length: > 0 } d ? d : null,
            logType);
    }

    private static InfraAuditEvent Generic(DateTimeOffset ts, JsonElement r, string logType)
    {
        var result = ResultOf(Str(r, "result"));
        if (result == InfraAuditResults.Info)
            result = ResultFromStatus(Int(r, "status"), Str(r, "level"));
        return new InfraAuditEvent(ts, Str(r, "component") ?? logType,
            Blank(Str(r, "user") ?? Str(r, "actor") ?? Str(r, "username")),
            Truncate(Mask(Str(r, "action") ?? Str(r, "event") ?? Str(r, "message") ?? logType) ?? logType, 160),
            Str(r, "target") ?? Str(r, "path"),
            result,
            Str(r, "source_ip") ?? Str(r, "remote_addr"), Str(r, "correlation_id"), null, logType);
    }

    /// <summary>Removes credentials from free text before it leaves the service.</summary>
    public static string? Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var masked = UrlUserInfo().Replace(value, "$1***@");
        masked = SecretAssignment().Replace(masked, "$1=***");
        masked = BearerToken().Replace(masked, "$1 ***");
        return masked;
    }

    private static string ResultFromStatus(int? status, string? level) => status switch
    {
        >= 200 and < 400 => InfraAuditResults.Allowed,
        401 or 403 => InfraAuditResults.Denied,
        >= 400 and < 500 => InfraAuditResults.Denied,
        >= 500 => InfraAuditResults.Error,
        _ => level?.ToLowerInvariant() is "error" or "fatal" ? InfraAuditResults.Error : InfraAuditResults.Info,
    };

    private static string ResultOf(string? result) => result?.ToLowerInvariant() switch
    {
        "pass" or "ok" or "success" or "allowed" or "allow" => InfraAuditResults.Allowed,
        "fail" or "denied" or "deny" or "rejected" => InfraAuditResults.Denied,
        "error" => InfraAuditResults.Error,
        _ => InfraAuditResults.Info,
    };

    private static string? Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => null,
        } : null;

    private static int? Int(JsonElement element, string name) =>
        int.TryParse(Str(element, name), out var parsed) ? parsed : null;

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) || value == "-" ? null : value;

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    [GeneratedRegex(@"([a-zA-Z][a-zA-Z0-9+.-]*://)[^/\s@]+@")]
    private static partial Regex UrlUserInfo();

    [GeneratedRegex(@"(?i)\b(password|passwd|pwd|secret|client_secret|token|access_token|refresh_token|api_key|apikey)\s*[=:]\s*[^\s,;&""']+")]
    private static partial Regex SecretAssignment();

    [GeneratedRegex(@"(?i)\b(bearer|basic)\s+[A-Za-z0-9._~+/=-]{8,}")]
    private static partial Regex BearerToken();
}
