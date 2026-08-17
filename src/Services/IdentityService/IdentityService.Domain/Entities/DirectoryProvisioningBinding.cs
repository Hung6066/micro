namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>Immutable target identity mapping used to make retries and updates idempotent.</summary>
public sealed class DirectoryProvisioningBinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Target { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
