namespace SystemDashboard.Bff.Models;

/// <summary>
/// Normalized alert record mapped from Prometheus AlertManager API.
/// </summary>
public sealed class AlertRecord
{
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = "firing";
    public string Severity { get; init; } = "info";
    public string Summary { get; init; } = string.Empty;
    public string Service { get; init; } = string.Empty;
    public string Instance { get; init; } = string.Empty;
    public DateTime StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public string GeneratorUrl { get; init; } = string.Empty;
    public bool IsSilenced { get; init; }
}
