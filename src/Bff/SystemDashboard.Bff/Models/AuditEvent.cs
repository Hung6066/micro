namespace SystemDashboard.Bff.Models;

public sealed record AuditEvent
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required string UserId { get; init; }
    public string UserName { get; init; } = "";
    public string Role { get; init; } = "";
    public required string Action { get; init; }
    public required string Resource { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public long DurationMs { get; init; }
    public int StatusCode { get; init; }
}
