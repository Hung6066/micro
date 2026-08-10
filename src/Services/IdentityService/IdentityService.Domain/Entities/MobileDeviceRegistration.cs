namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>Durable device registration for push delivery and revocation.</summary>
public sealed class MobileDeviceRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string TokenCiphertext { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
}
