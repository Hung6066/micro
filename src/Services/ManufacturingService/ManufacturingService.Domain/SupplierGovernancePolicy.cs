namespace His.Hope.ManufacturingService.Domain;

public static class SupplierGovernancePolicy
{
    private static readonly string[] ApprovalStatuses = [ManufacturingStatusCodes.Draft, ManufacturingStatusCodes.PendingApproval, ManufacturingStatusCodes.Approved, ManufacturingStatusCodes.Suspended, ManufacturingStatusCodes.Rejected];
    private static readonly string[] RiskLevels = ["Low", "Standard", "High", "Critical"];

    public static string? ValidateProfile(string approvalStatus, string riskLevel, string? countryCode, string? contactEmail)
    {
        if (!ApprovalStatuses.Contains(approvalStatus, StringComparer.OrdinalIgnoreCase)) return "invalid_supplier_approval_status";
        if (!RiskLevels.Contains(riskLevel, StringComparer.OrdinalIgnoreCase)) return "invalid_supplier_risk_level";
        if (!string.IsNullOrWhiteSpace(countryCode) && (countryCode.Length != 2 || countryCode.Any(character => !char.IsLetter(character)))) return "invalid_supplier_country_code";
        if (!string.IsNullOrWhiteSpace(contactEmail) && !contactEmail.Contains('@', StringComparison.Ordinal)) return "invalid_supplier_contact_email";
        return null;
    }

    public static string? ValidateApprovalTransition(string currentStatus, string nextStatus)
    {
        if (!ApprovalStatuses.Contains(nextStatus, StringComparer.OrdinalIgnoreCase)) return "invalid_supplier_approval_status";
        if (currentStatus.Equals(nextStatus, StringComparison.OrdinalIgnoreCase)) return null;

        return (currentStatus, nextStatus) switch
        {
            (ManufacturingStatusCodes.Draft, ManufacturingStatusCodes.PendingApproval) or (ManufacturingStatusCodes.Draft, ManufacturingStatusCodes.Rejected) or
            (ManufacturingStatusCodes.PendingApproval, ManufacturingStatusCodes.Approved) or (ManufacturingStatusCodes.PendingApproval, ManufacturingStatusCodes.Rejected) or
            (ManufacturingStatusCodes.Approved, ManufacturingStatusCodes.Suspended) or (ManufacturingStatusCodes.Suspended, ManufacturingStatusCodes.Approved) or
            (ManufacturingStatusCodes.Rejected, ManufacturingStatusCodes.Draft) => null,
            _ => "invalid_supplier_approval_transition"
        };
    }

    public static bool IsPurchasable(string approvalStatus) => approvalStatus.Equals(ManufacturingStatusCodes.Approved, StringComparison.OrdinalIgnoreCase);
}
