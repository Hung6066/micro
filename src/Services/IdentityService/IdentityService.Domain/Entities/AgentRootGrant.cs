namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>
/// A four-eyes, time-limited grant of root SSH to the infrastructure automation agent.
/// Approval makes IdentityService sign the agent's public key with the Vault SSH CA; the
/// certificate itself expires at <see cref="ValidBefore"/> regardless of what happens here.
/// </summary>
public class AgentRootGrant
{
    public const string StatusPending = "pending";
    public const string StatusIssued = "issued";
    public const string StatusRevoked = "revoked";
    public const string StatusExpired = "expired";
    public const string StatusFailed = "failed";

    public Guid Id { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string Status { get; set; } = StatusPending;
    public Guid RequestedBy { get; set; }
    public string RequestedByEmail { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public string? ApprovedByEmail { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ValidBefore { get; set; }
    public string? CertificateSerial { get; set; }

    /// <summary>The signed OpenSSH certificate. Never returned by list endpoints or written to audit.</summary>
    public string? Certificate { get; set; }

    public Guid? RevokedBy { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? FailureReason { get; set; }
}
