namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>Periodic certification record for a privileged role assignment.</summary>
public sealed class AccessReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubjectUserId { get; set; }
    public string Reviewer { get; set; } = string.Empty;
    public string RoleIdsJson { get; set; } = "[]";
    public string Status { get; set; } = "pending";
    public string? DecisionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime DueAt { get; set; } = DateTime.UtcNow.AddDays(30);
    public DateTime? DecidedAt { get; set; }
}
