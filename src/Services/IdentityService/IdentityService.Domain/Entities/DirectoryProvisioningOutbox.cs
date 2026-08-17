namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>Durable, idempotent change queue for outbound directory provisioning.</summary>
public sealed class DirectoryProvisioningOutbox
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Target { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime AvailableAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int Attempts { get; set; }
    public string? ExternalId { get; set; }
    public string? LastError { get; set; }
}
