namespace His.Hope.IdentityService.Domain.Entities;

public class ClientConsent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? RevokedAt { get; set; }
}
