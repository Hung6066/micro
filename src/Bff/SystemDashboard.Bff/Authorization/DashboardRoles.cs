namespace SystemDashboard.Bff.Authorization;

public static class DashboardRoles
{
    public const string Admin = "admin";
    public const string Operator = "operator";
    public const string Viewer = "viewer";
    public const string ReadOnly = "admin,operator,viewer";
    public const string Manage = "admin,operator";
}
