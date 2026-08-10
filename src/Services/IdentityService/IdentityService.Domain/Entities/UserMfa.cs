namespace His.Hope.IdentityService.Domain.Entities;

public class UserMfa
{
    public Guid UserId { get; set; }
    public string SecretKey { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime? EnrolledAt { get; set; }
    public string[] RecoveryCodes { get; set; } = [];
    public int BackupCodesUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
