namespace SystemDashboard.Bff.Models;

public sealed record EnvironmentContext
{
    public required string Name { get; init; }
    public DateTime SwitchedAt { get; init; } = DateTime.UtcNow;
}
