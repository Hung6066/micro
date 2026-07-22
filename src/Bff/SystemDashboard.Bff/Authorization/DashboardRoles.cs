namespace SystemDashboard.Bff.Authorization;

/// <summary>
/// Dashboard role constants matching IdentityService seeded roles (PascalCase).
/// ASP.NET Core authorization is case-insensitive by default for ClaimTypes.Role.
/// </summary>
public static class DashboardRoles
{
    // Identity service role names (PascalCase)
    public const string Admin = "Admin";
    public const string Provider = "Provider";
    public const string Nurse = "Nurse";
    public const string Receptionist = "Receptionist";
    public const string LabTechnician = "LabTechnician";
    public const string Pharmacist = "Pharmacist";
    public const string BillingClerk = "BillingClerk";

    // Composite role groups
    // All clinical+admin roles can view dashboard
    public const string ReadOnly = "Admin,Provider,Nurse,Receptionist,LabTechnician,Pharmacist,BillingClerk";
    // Only Admin can manage service lifecycle
    public const string Manage = "Admin";
}
