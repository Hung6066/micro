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

    private static readonly Regex MrnRegex = new(
        @"(?i)\b(MRN|medical\s+record\s+(number|no\.?))\s*[:#=]?\s*[A-Z0-9-]{5,}\b",
        RegexOptions.Compiled);

    private static readonly Regex DateOfBirthRegex = new(
        @"(?i)\b(DOB|date\s+of\s+birth|birth\s+date)\s*[:=]?\s*\d{1,2}[/-]\d{1,2}[/-]\d{2,4}\b",
        RegexOptions.Compiled);

    private static readonly Regex PatientNameRegex = new(
        @"(?i)\b(patient|member|subscriber)\s+(name|full\s+name)\s*[:=]\s*[A-Z][A-Za-z'\-]+(?:\s+[A-Z][A-Za-z'\-]+){1,3}\b",
        RegexOptions.Compiled);

    private static readonly Regex HumanNameContextRegex = new(
        @"(?i)\b(name|patient|provider|doctor|physician)\s*[:=]\s*[A-Z][A-Za-z'\-]+(?:\s+[A-Z][A-Za-z'\-]+){1,3}\b",
        RegexOptions.Compiled);

    private static readonly Regex AddressRegex = new(
        @"(?i)\b\d{1,6}\s+[A-Z][A-Za-z0-9'\-.]*(?:\s+[A-Z][A-Za-z0-9'\-.]*){0,5}\s+(street|st\.?|avenue|ave\.?|road|rd\.?|drive|dr\.?|lane|ln\.?|boulevard|blvd\.?)\b",
        RegexOptions.Compiled);

    public string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        var result = text;

        result = MrnRegex.Replace(result, m => ReplaceValueAfterSeparator(m.Value, "[REDACTED_MRN]"));
        result = DateOfBirthRegex.Replace(result, m => ReplaceValueAfterSeparator(m.Value, "[REDACTED_DOB]"));
        result = PatientNameRegex.Replace(result, m => ReplaceValueAfterSeparator(m.Value, "[REDACTED_NAME]"));
        result = HumanNameContextRegex.Replace(result, m => ReplaceValueAfterSeparator(m.Value, "[REDACTED_NAME]"));
        result = AddressRegex.Replace(result, "[REDACTED_ADDRESS]");
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

    private static string ReplaceValueAfterSeparator(string value, string replacement)
    {
        var separatorIndex = value.IndexOfAny([':', '=']);
        if (separatorIndex < 0)
        {
            separatorIndex = value.IndexOf('#');
        }

        if (separatorIndex < 0)
            return replacement;

        return value[..(separatorIndex + 1)] + replacement;
    }
}
