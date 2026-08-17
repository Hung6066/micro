namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>Maker-checker request for a role assignment change.</summary>
public sealed class AccessRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubjectUserId { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public string RoleIdsJson { get; set; } = "[]";
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string? ApprovedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);
}
