using System.Collections.Frozen;

namespace His.Hope.Infrastructure.Security.Authorization.Handlers;

/// <summary>
/// Maps role names to their corresponding permission codes.
/// This provides a fallback authorization mechanism when the JWT token
/// does not contain explicit permission claims (legacy/transitional mode).
///
/// In production, permissions should always be embedded in the JWT by IdentityService.
/// This mapping should be kept in sync with the database seed data.
/// </summary>
public static class RolePermissionMapping
{
    private static readonly FrozenDictionary<string, FrozenSet<string>> RolePermissions = new Dictionary<string, FrozenSet<string>>
    {
        ["Admin"] = His.Hope.SharedKernel.Authorization.HisHopePermissions.All,

        ["Provider"] = new HashSet<string>
        {
            "patients.view", "patients.create", "patients.update",
            "appointments.view", "appointments.create", "appointments.update", "appointments.cancel",
            "clinical.view", "clinical.create", "clinical.update", "clinical.sign",
            "lab.view", "lab.create",
            "pharmacy.view", "pharmacy.create", "pharmacy.dispense",
        }.ToFrozenSet(),

        ["Nurse"] = new HashSet<string>
        {
            "patients.view", "patients.update",
            "appointments.view", "appointments.check-in",
            "clinical.view", "clinical.create", "clinical.update",
            "lab.view",
        }.ToFrozenSet(),

        ["Receptionist"] = new HashSet<string>
        {
            "patients.view", "patients.create",
            "appointments.view", "appointments.create", "appointments.check-in",
            "billing.view", "billing.create",
        }.ToFrozenSet(),

        ["LabTechnician"] = new HashSet<string>
        {
            "lab.view", "lab.create", "lab.update", "lab.result",
            "patients.view",
        }.ToFrozenSet(),

        ["Pharmacist"] = new HashSet<string>
        {
            "pharmacy.view", "pharmacy.update", "pharmacy.dispense",
            "patients.view",
        }.ToFrozenSet(),

        ["BillingClerk"] = new HashSet<string>
        {
            "billing.view", "billing.create", "billing.update", "billing.void",
            "patients.view",
        }.ToFrozenSet(),
    }.ToFrozenDictionary();

    /// <summary>
    /// Gets all permissions assigned to the specified roles.
    /// </summary>
    public static FrozenSet<string> GetPermissionsForRoles(IEnumerable<string> roles)
    {
        var permissions = new HashSet<string>();
        foreach (var role in roles)
        {
            if (RolePermissions.TryGetValue(role, out var rolePerms))
            {
                permissions.UnionWith(rolePerms);
            }
        }
        return permissions.ToFrozenSet();
    }

    /// <summary>
    /// Returns true if any of the specified roles has the given permission.
    /// </summary>
    public static bool RoleHasPermission(IEnumerable<string> roles, string permissionCode)
    {
        return roles.Any(role =>
            RolePermissions.TryGetValue(role, out var perms) &&
            perms.Contains(permissionCode));
    }
}
