namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>Normalized, short-lived device posture evidence. Raw vendor proofs are never persisted.</summary>
public sealed class DevicePostureAssessment
{
    public string ScopeId { get; set; } = IdentityScope.Global;
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string EvidenceHash { get; set; } = string.Empty;
    public string SignalsJson { get; set; } = "{}";
    public DateTime ObservedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public string PolicyVersion { get; set; } = "1";
    public string Decision { get; set; } = "observe";
    public string? CorrelationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
