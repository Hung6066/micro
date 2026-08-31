using System.Text.Json;
using His.Hope.SharedKernel.Protocol;

namespace His.Hope.Authorization;

internal sealed record ResourcePolicyClaim(
    string ServiceKey,
    string ResourcePattern,
    string Effect,
    IReadOnlyList<string> Actions,
    JsonElement? Condition = null);

/// <summary>
/// Applies the published resource-policy statements projected by Identity
/// Service into the token. A matching deny wins; when a policy matches the
/// resource/action, an explicit allow is required.
/// </summary>
internal static class ResourcePolicyEvaluator
{
    private static readonly JsonSerializerOptions ClaimJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string? Evaluate(System.Security.Claims.ClaimsPrincipal principal, AuthorizationResource resource, string action)
    {
        var claims = principal.FindAll("resource_policies").ToArray();
        if (claims.Length == 0) return null;

        var matched = false;
        var allowed = false;
        foreach (var claim in claims)
        {
            ResourcePolicyClaim[] statements;
            try
            {
                statements = JsonSerializer.Deserialize<ResourcePolicyClaim[]>(claim.Value, ClaimJsonOptions) ?? [];
            }
            catch (JsonException)
            {
                return "resource_policy_invalid";
            }

            foreach (var statement in statements)
            {
                if (!ActionMatches(statement, action) || !PatternMatches(statement.ResourcePattern, resource) || !ConditionMatches(statement.Condition, principal, resource))
                    continue;

                matched = true;
                if (string.Equals(statement.Effect, "deny", StringComparison.OrdinalIgnoreCase))
                    return "resource_policy_denied";
                if (string.Equals(statement.Effect, "allow", StringComparison.OrdinalIgnoreCase))
                    allowed = true;
                else
                    return "resource_policy_invalid";
            }
        }

        return matched && !allowed ? "resource_policy_denied" : null;
    }

    private static bool ActionMatches(ResourcePolicyClaim statement, string action) =>
        statement.Actions.Any(candidate =>
            candidate == "*" || string.Equals(candidate, action, StringComparison.OrdinalIgnoreCase));

    private static bool PatternMatches(string pattern, AuthorizationResource resource)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;
        if (pattern == "*") return true;
        if (pattern.EndsWith("/*", StringComparison.Ordinal))
        {
            var prefix = pattern[..^2];
            return resource.CanonicalId.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase) ||
                resource.CanonicalId.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(resource.Type, prefix, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(pattern, resource.CanonicalId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(pattern, resource.Type, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ConditionMatches(JsonElement? condition, System.Security.Claims.ClaimsPrincipal principal, AuthorizationResource resource)
    {
        if (condition is null) return true;
        var root = condition.Value;
        if (root.ValueKind != JsonValueKind.Object) return false;

        foreach (var operatorProperty in root.EnumerateObject())
        {
            if (operatorProperty.Value.ValueKind != JsonValueKind.Object) return false;
            foreach (var property in operatorProperty.Value.EnumerateObject())
            {
                var expected = property.Value.ValueKind switch
                {
                    JsonValueKind.String => [property.Value.GetString() ?? string.Empty],
                    JsonValueKind.Array => property.Value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? string.Empty).ToArray(),
                    _ => Array.Empty<string>()
                };
                if (expected.Length == 0 || expected.Any(string.IsNullOrWhiteSpace)) return false;
                var actual = ResolveConditionValue(property.Name, principal, resource);
                if (actual is null || !EvaluateCondition(operatorProperty.Name, actual, expected)) return false;
            }
        }
        return true;
    }

    private static string? ResolveConditionValue(string key, System.Security.Claims.ClaimsPrincipal principal, AuthorizationResource resource) =>
        key.ToLowerInvariant() switch
        {
            HisHopeProtocolConstants.Claims.TenantId or HisHopeProtocolConstants.Claims.Tenant =>
                principal.FindFirst(HisHopeProtocolConstants.Claims.TenantId)?.Value
                ?? principal.FindFirst(HisHopeProtocolConstants.Claims.Tenant)?.Value
                ?? resource.TenantId,
            "facility_id" or "facility" => resource.FacilityId,
            "resource_type" or "resourcetype" => resource.Type,
            "lifecycle_state" or "lifecyclestate" => resource.LifecycleState,
            "principal_type" or "principaltype" => principal.FindFirst("principal_type")?.Value,
            _ => principal.FindFirst(key)?.Value
        };

    private static bool EvaluateCondition(string operatorName, string actual, IReadOnlyList<string> expected)
    {
        var op = operatorName.ToLowerInvariant();
        if (op is "stringequals" or "arnequals")
            return expected.Any(value => string.Equals(actual, value, StringComparison.OrdinalIgnoreCase));
        if (op is "stringnotequals" or "arnnotequals")
            return expected.All(value => !string.Equals(actual, value, StringComparison.OrdinalIgnoreCase));
        if (op is "stringlike" or "arnlike")
            return expected.Any(value => WildcardMatch(actual, value));
        if (op is "stringnotlike" or "arnnotlike")
            return expected.All(value => !WildcardMatch(actual, value));
        if (op == "bool")
            return expected.Any(value => bool.TryParse(actual, out var actualBool) && bool.TryParse(value, out var expectedBool) && actualBool == expectedBool);
        if (op.StartsWith("numeric", StringComparison.Ordinal))
            return CompareNumbers(op[7..], actual, expected);
        if (op.StartsWith("date", StringComparison.Ordinal))
            return CompareDates(op[4..], actual, expected);
        return false;
    }

    private static bool WildcardMatch(string actual, string pattern)
    {
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(actual, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static bool CompareNumbers(string comparison, string actual, IReadOnlyList<string> expected) =>
        decimal.TryParse(actual, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var actualNumber) &&
        expected.Any(value => decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var expectedNumber) && Compare(comparison, actualNumber, expectedNumber));

    private static bool CompareDates(string comparison, string actual, IReadOnlyList<string> expected) =>
        DateTimeOffset.TryParse(actual, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var actualDate) &&
        expected.Any(value => DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var expectedDate) && Compare(comparison, actualDate, expectedDate));

    private static bool Compare<T>(string comparison, T actual, T expected) where T : IComparable<T> =>
        comparison switch
        {
            "equals" => actual.CompareTo(expected) == 0,
            "lessthan" => actual.CompareTo(expected) < 0,
            "lessthanequals" => actual.CompareTo(expected) <= 0,
            "greaterthan" => actual.CompareTo(expected) > 0,
            "greaterthanequals" => actual.CompareTo(expected) >= 0,
            _ => false
        };
}
