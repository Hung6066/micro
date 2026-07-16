namespace His.Hope.IdentityService.Domain.Entities;

/// <summary>
/// Represents a security audit log entry for HIPAA compliance (164.312(b)).
/// Records every PHI access event with who, what, when, and where.
/// In addition to the existing Serilog-based audit, this entity provides
/// a queryable, persistent audit trail in the database for compliance reporting.
/// </summary>
public class AuditLog
{
    /// <summary>
    /// Unique identifier for the audit entry.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// User ID from the JWT 'sub' claim (who accessed the data).
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// User display name at time of access (denormalized for queryability).
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Action performed (e.g., "READ", "CREATE", "UPDATE", "DELETE").
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Type of resource accessed (e.g., "Patient", "Encounter", "LabOrder", "User", "Setting").
    /// </summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// ID of the specific resource that was accessed.
    /// </summary>
    public string? ResourceId { get; set; }

    /// <summary>
    /// Additional details about the audit event (context-specific).
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Client IP address from X-Forwarded-For or RemoteIp.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User-Agent header from the HTTP request.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// UTC timestamp when the access occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
