using System.Text.Json;

namespace His.Hope.Authorization;

internal sealed record ResourcePolicyClaim(
    string ServiceKey,
    string ResourcePattern,
    string Effect,
    IReadOnlyList<string> Actions);

/// <summary>
/// Applies the published resource-policy statements projected by Identity
/// Service into the token. A matching deny wins; when a policy matches the
/// resource/action, an explicit allow is required.
/// </summary>
internal static class ResourcePolicyEvaluator
{
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
                statements = JsonSerializer.Deserialize<ResourcePolicyClaim[]>(claim.Value) ?? [];
            }
            catch (JsonException)
            {
                return "resource_policy_invalid";
            }

            foreach (var statement in statements)
            {
                if (!ActionMatches(statement, action) || !PatternMatches(statement.ResourcePattern, resource))
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
}
