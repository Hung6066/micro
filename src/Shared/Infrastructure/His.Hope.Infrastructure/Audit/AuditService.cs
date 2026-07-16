using Serilog;

namespace His.Hope.Infrastructure.Audit;

/// <summary>
/// Service for recording PHI (Protected Health Information) access audit events.
///
/// HIPAA Context (164.312(b)) - Audit Controls:
///   "Implement hardware, software, and/or procedural mechanisms that record
///    and examine access and other activity in information systems that contain
///    or use electronic protected health information."
///
/// Every PHI access is logged with structured properties for:
///   - Who accessed the data (UserId)
///   - What was accessed (ResourceType, ResourceId, Action)
///   - When it was accessed (Timestamp)
///   - Where it was accessed from (ClientIp, UserAgent)
///
/// These logs feed into the ELK stack for automated analysis and breach detection.
/// </summary>
public class AuditService : IAuditService
{
    private readonly ILogger _logger;

    public AuditService()
    {
        _logger = Log.ForContext<AuditService>();
    }

    /// <summary>
    /// Records a PHI access audit event with structured properties.
    /// Each event is logged as a separate structured log entry for
    /// searchability in ELK and compliance reporting.
    /// </summary>
    public void LogPhiAccess(PhiAuditEntry entry)
    {
        // SECURITY: Never log PHI values (like diagnosis details, lab results).
        // Only log metadata about the access event.
        _logger
            .ForContext("AuditType", "PHI_ACCESS")
            .ForContext("UserId", entry.UserId)
            .ForContext("UserRole", entry.UserRole)
            .ForContext("ResourceType", entry.ResourceType)
            .ForContext("ResourceId", entry.ResourceId)
            .ForContext("Action", entry.Action)
            .ForContext("Timestamp", entry.Timestamp)
            .ForContext("ClientIp", entry.ClientIp)
            .ForContext("UserAgent", entry.UserAgent)
            .ForContext("CorrelationId", entry.CorrelationId)
            .ForContext("TenantId", entry.TenantId)
            .ForContext("HttpMethod", entry.HttpMethod)
            .ForContext("Path", entry.Path)
            .Information("PHI access: {Action} on {ResourceType}:{ResourceId} by user {UserId} from {ClientIp}");
    }

    /// <summary>
    /// Records an audit event asynchronously.
    /// In production, this should write to a dedicated audit log sink.
    /// </summary>
    public Task LogPhiAccessAsync(PhiAuditEntry entry, CancellationToken ct = default)
    {
        LogPhiAccess(entry);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Structured audit entry for PHI access events.
/// Contains all metadata required for HIPAA audit compliance.
/// </summary>
public record PhiAuditEntry
{
    /// <summary>User ID from JWT 'sub' claim</summary>
    public required string UserId { get; init; }

    /// <summary>User role from JWT 'role' claim</summary>
    public string? UserRole { get; init; }

    /// <summary>Type of resource accessed (e.g., "Patient", "Encounter", "LabOrder")</summary>
    public required string ResourceType { get; init; }

    /// <summary>ID of the specific resource accessed</summary>
    public required string ResourceId { get; init; }

    /// <summary>Action performed (e.g., "CREATE", "READ", "UPDATE", "DELETE")</summary>
    public required string Action { get; init; }

    /// <summary>UTC timestamp of the access event</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Client IP address (from X-Forwarded-For or RemoteIp)</summary>
    public string? ClientIp { get; init; }

    /// <summary>User-Agent header from the request</summary>
    public string? UserAgent { get; init; }

    /// <summary>Correlation ID for distributed tracing</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Tenant identifier for multi-tenant isolation</summary>
    public string? TenantId { get; init; }

    /// <summary>HTTP method (GET, POST, PUT, DELETE, PATCH)</summary>
    public string? HttpMethod { get; init; }

    /// <summary>Request path</summary>
    public string? Path { get; init; }
}

/// <summary>
/// Interface for the PHI audit service.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Logs a PHI access audit event synchronously.
    /// </summary>
    void LogPhiAccess(PhiAuditEntry entry);

    /// <summary>
    /// Logs a PHI access audit event asynchronously.
    /// </summary>
    Task LogPhiAccessAsync(PhiAuditEntry entry, CancellationToken ct = default);
}
