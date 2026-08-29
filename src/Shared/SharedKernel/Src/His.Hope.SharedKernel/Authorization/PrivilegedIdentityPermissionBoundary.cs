namespace His.Hope.SharedKernel.Authorization;

/// <summary>Limits configured privileged identities to control-plane permissions.</summary>
public static class PrivilegedIdentityPermissionBoundary
{
    private static readonly string[] ControlPlanePrefixes =
    ["admin.", "identity.", "security.", "audit.", "access.", "facility."];

    public static bool IsControlPlanePermission(string permission) =>
        ControlPlanePrefixes.Any(prefix => permission.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<string> Filter(IEnumerable<string> permissions) =>
        permissions.Where(IsControlPlanePermission).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
