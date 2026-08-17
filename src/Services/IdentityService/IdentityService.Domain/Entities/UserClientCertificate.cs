namespace His.Hope.IdentityService.Domain.Entities;

public sealed class UserClientCertificate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Thumbprint { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public DateTime NotAfter { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}
