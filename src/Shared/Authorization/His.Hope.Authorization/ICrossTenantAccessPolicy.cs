namespace His.Hope.Authorization;

public interface ICrossTenantAccessPolicy
{
    bool IsCrossTenantAllowed(string sourceTenant, string targetTenant, string action);
}

public sealed class DefaultDenyCrossTenantAccessPolicy : ICrossTenantAccessPolicy
{
    public bool IsCrossTenantAllowed(string sourceTenant, string targetTenant, string action) => false;
}

public sealed record CrossTenantAllowedPair(
    string Source,
    string? Target,
    string Reason,
    IReadOnlyList<string> Permissions,
    string? TargetClass = null,
    bool OperatorHomeMatch = false,
    bool RequiresJit = false,
    int MaxDurationMinutes = 60);

public interface ICrossTenantTenantMetadata
{
    string GetTenantClass(string tenantKey);

    string? GetOperatorHome(string tenantKey);
}

public sealed class ConfigurableCrossTenantAccessPolicy : ICrossTenantAccessPolicy
{
    private readonly IReadOnlyList<CrossTenantAllowedPair> _allowedPairs;
    private readonly ICrossTenantTenantMetadata? _tenantMetadata;

    public ConfigurableCrossTenantAccessPolicy(
        IEnumerable<CrossTenantAllowedPair> allowedPairs,
        ICrossTenantTenantMetadata? tenantMetadata = null)
    {
        _allowedPairs = allowedPairs.ToList();
        _tenantMetadata = tenantMetadata;
    }

    public bool IsCrossTenantAllowed(string sourceTenant, string targetTenant, string action) =>
        FindMatchingPair(sourceTenant, targetTenant, action, requiresJit: null) is not null;

    public CrossTenantAllowedPair? FindMatchingPair(
        string sourceTenant,
        string targetTenant,
        string action,
        bool? requiresJit)
    {
        foreach (var pair in _allowedPairs)
        {
            if (!string.Equals(pair.Source, sourceTenant, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!MatchesTarget(pair, targetTenant))
                continue;

            if (pair.OperatorHomeMatch &&
                _tenantMetadata is not null &&
                !string.Equals(_tenantMetadata.GetOperatorHome(targetTenant), sourceTenant, StringComparison.OrdinalIgnoreCase))
                continue;

            if (requiresJit.HasValue && pair.RequiresJit != requiresJit.Value)
                continue;

            if (pair.Permissions.Count > 0 &&
                !pair.Permissions.Any(permission =>
                    string.Equals(permission, action, StringComparison.OrdinalIgnoreCase)))
                continue;

            return pair;
        }

        return null;
    }

    private bool MatchesTarget(CrossTenantAllowedPair pair, string targetTenant)
    {
        if (!string.IsNullOrWhiteSpace(pair.Target))
            return string.Equals(pair.Target, targetTenant, StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(pair.TargetClass) || _tenantMetadata is null)
            return false;

        return string.Equals(
            _tenantMetadata.GetTenantClass(targetTenant),
            pair.TargetClass,
            StringComparison.OrdinalIgnoreCase);
    }
}
