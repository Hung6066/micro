namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>Durable, bounded mobile telemetry record. Payloads must not contain PHI.</summary>
public sealed class MobileTelemetryEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Stack { get; set; }
    public string? Route { get; set; }
    public string AppVersion { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public double? DurationMs { get; set; }
    public string? MetadataJson { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
