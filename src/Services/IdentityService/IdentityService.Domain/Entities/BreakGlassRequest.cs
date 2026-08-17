namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>
/// An auditable, time-bounded emergency access request. A request never grants
/// access by itself; approval and expiry are evaluated server-side.
/// </summary>
public class BreakGlassRequest
{
    public Guid Id { get; set; }
    public Guid SubjectUserId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string RequestedBy { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
