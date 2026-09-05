namespace His.Hope.IdentityService.Application.Authorization;

/// <summary>
/// Static separation-of-duty constraints for high-impact clinical workflows.
/// The check runs before replacing a user's role set and fails closed.
/// </summary>
public static class RoleSeparationOfDuties
{
    private static readonly (string Left, string Right)[] Conflicts =
    [
        ("Provider", "BillingClerk"),
        ("Pharmacist", "BillingClerk"),
        // Manufacturing four-eyes controls: the person executing or
        // inspecting a process must not be its final approver.
        ("ManufacturingProductionOperator", "ManufacturingQualityManager"),
        ("ManufacturingQualityInspector", "ManufacturingQualityManager"),
        ("ManufacturingProductionOperator", "ManufacturingPlantManager"),
        ("ManufacturingQualityInspector", "ManufacturingPlantManager"),
        ("ManufacturingRecipeManager", "ManufacturingProductionOperator"),
        ("ManufacturingCostController", "ManufacturingPlantManager")
    ];

    public static bool TryFindConflict(IEnumerable<string> roleNames, out string conflict)
    {
        var normalized = roleNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (left, right) in Conflicts)
        {
            if (normalized.Contains(left) && normalized.Contains(right))
            {
                conflict = $"{left} + {right}";
                return true;
            }
        }

        conflict = string.Empty;
        return false;
    }
}
