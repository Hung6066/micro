namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>
/// Persisted four-eyes request for a high-risk authorization change.
/// The request is immutable in intent: approval never changes the captured
/// target and execution is a separate, auditable transition.
/// </summary>
public sealed class AuthorizationChangeRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ResourceType { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = "pending";
    public string? ApprovedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);
}
