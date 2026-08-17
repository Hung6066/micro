using System.Collections.Frozen;

namespace His.Hope.Authorization.Handlers;

public static class RolePermissionMapping
{
    private static readonly FrozenDictionary<string, FrozenSet<string>> RolePermissions = new Dictionary<string, FrozenSet<string>>
    {
        ["Admin"] = His.Hope.SharedKernel.Authorization.HisHopePermissions.All,
        ["Provider"] = new HashSet<string>
        {
            "patients.view", "patients.create", "patients.update", "appointments.view", "appointments.create", "appointments.update", "appointments.cancel",
            "clinical.view", "clinical.create", "clinical.update", "clinical.sign", "lab.view", "lab.create", "pharmacy.view", "pharmacy.create", "pharmacy.dispense", "dashboard.view",
        }.ToFrozenSet(),
        ["Nurse"] = new HashSet<string>
        {
            "patients.view", "patients.update", "appointments.view", "appointments.check-in", "clinical.view", "clinical.create", "clinical.update", "lab.view", "dashboard.view",
        }.ToFrozenSet(),
        ["Receptionist"] = new HashSet<string>
        {
            "patients.view", "patients.create", "appointments.view", "appointments.create", "appointments.check-in", "billing.view", "billing.create", "dashboard.view",
        }.ToFrozenSet(),
        ["LabTechnician"] = new HashSet<string> { "lab.view", "lab.create", "lab.update", "lab.result", "lab.alert.acknowledge", "lab.alert.resolve", "patients.view", "dashboard.view" }.ToFrozenSet(),
        ["Pharmacist"] = new HashSet<string> { "pharmacy.view", "pharmacy.update", "pharmacy.dispense", "patients.view", "dashboard.view" }.ToFrozenSet(),
        ["BillingClerk"] = new HashSet<string> { "billing.view", "billing.create", "billing.update", "billing.void", "patients.view", "dashboard.view" }.ToFrozenSet(),
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static FrozenSet<string> GetPermissionsForRoles(IEnumerable<string> roles)
    {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
            if (RolePermissions.TryGetValue(role, out var rolePermissions)) permissions.UnionWith(rolePermissions);
        return permissions.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }
}
