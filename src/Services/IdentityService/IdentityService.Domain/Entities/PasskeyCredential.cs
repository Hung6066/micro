namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>Durable WebAuthn credential material. Challenges remain short-lived in Redis.</summary>
public sealed class PasskeyCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string CredentialId { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public uint SignatureCounter { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
}
