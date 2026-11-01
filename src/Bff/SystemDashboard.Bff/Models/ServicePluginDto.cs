namespace SystemDashboard.Bff.Models;

public sealed record ServicePluginDto(
    string Key,
    string DisplayName,
    string? DashboardRoute,
    string? Icon,
    string[] Permissions);
