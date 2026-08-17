using Microsoft.AspNetCore.Authorization;

namespace His.Hope.Authorization.Requirements;

/// <summary>
/// Restricts a policy to an explicitly typed principal. The claim is issued by
/// IdentityService and is never inferred from a role or a client id at the
/// resource boundary.
/// </summary>
public sealed class PrincipalTypeRequirement : IAuthorizationRequirement
{
    public PrincipalTypeRequirement(params string[] principalTypes)
    {
        PrincipalTypes = principalTypes
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Select(type => type.Trim())
            .ToHashSet(StringComparer.Ordinal);
        if (PrincipalTypes.Count == 0)
            throw new ArgumentException("At least one principal type is required.", nameof(principalTypes));
    }

    public IReadOnlySet<string> PrincipalTypes { get; }
}
