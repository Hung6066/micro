using Microsoft.AspNetCore.Authorization;

namespace His.Hope.Authorization.Requirements;

/// <summary>
/// Requires at least one explicit OAuth scope on the access token.
/// Scope claims may be space-delimited (RFC 9068/OAuth) or emitted as
/// multiple claims by an upstream token handler.
/// </summary>
public sealed class ScopeRequirement : IAuthorizationRequirement
{
    public ScopeRequirement(params string[] scopes)
    {
        Scopes = scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (Scopes.Count == 0)
            throw new ArgumentException("At least one scope is required.", nameof(scopes));
    }

    public IReadOnlyList<string> Scopes { get; }
}
