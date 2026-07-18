using System.Text.RegularExpressions;

namespace His.Hope.AgentHarness.Application.Services;

public class PiiRedactionService
{
    private static readonly Regex EmailRegex = new(
        @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled);

    private static readonly Regex PhoneRegex = new(
        @"\+?\d{1,4}[-.\s]?\(?\d{1,4}\)?[-.\s]?\d{1,4}[-.\s]?\d{1,9}",
        RegexOptions.Compiled);

    private static readonly Regex SsnRegex = new(
        @"\b\d{3}-\d{2}-\d{4}\b",
        RegexOptions.Compiled);

    private static readonly Regex IpRegex = new(
        @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b",
        RegexOptions.Compiled);

    private static readonly Regex JwtRegex = new(
        @"[A-Za-z0-9-_]{20,}\.[A-Za-z0-9-_]{20,}\.[A-Za-z0-9-_]{20,}",
        RegexOptions.Compiled);

    private static readonly Regex CredentialRegex = new(
        @"(?i)(password|secret)\s*[:=]\s*\S+",
        RegexOptions.Compiled);

    public string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        var result = text;

        result = EmailRegex.Replace(result, "[REDACTED_EMAIL]");
        result = PhoneRegex.Replace(result, "[REDACTED_PHONE]");
        result = SsnRegex.Replace(result, "[REDACTED_SSN]");
        result = IpRegex.Replace(result, "[REDACTED_IP]");
        result = JwtRegex.Replace(result, "[REDACTED_TOKEN]");
        result = CredentialRegex.Replace(result, m =>
        {
            var key = m.Groups[1].Value;
            var afterKey = m.Value[key.Length..];
            var sep = afterKey.Length > 0 && (afterKey[0] == ':' || afterKey[0] == '=')
                ? afterKey[0].ToString()
                : "";
            return $"{key}{sep}[REDACTED_CREDENTIAL]";
        });

        return result;
    }
}
