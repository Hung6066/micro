namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>One-way password hash retained for password-reuse compliance checks.</summary>
public sealed class UserPasswordHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
