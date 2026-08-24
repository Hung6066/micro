using Microsoft.AspNetCore.Authorization;

namespace His.Hope.Authorization.Requirements;

/// <summary>
/// Requires an explicit <c>portal_class</c> claim matching one of the allowed values.
/// </summary>
public sealed class PortalClassRequirement : IAuthorizationRequirement
{
    public PortalClassRequirement(params string[] allowedPortalClasses)
    {
        AllowedPortalClasses = allowedPortalClasses
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (AllowedPortalClasses.Count == 0)
            throw new ArgumentException("At least one portal class is required.", nameof(allowedPortalClasses));
    }

    public IReadOnlyList<string> AllowedPortalClasses { get; }
}
