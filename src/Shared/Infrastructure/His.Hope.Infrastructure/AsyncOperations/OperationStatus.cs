namespace His.Hope.Infrastructure.AsyncOperations;

/// <summary>
/// Represents the status of a long-running asynchronous operation.
///
/// When a client sends a request with the <c>Prefer: respond-async</c> header,
/// the middleware creates an <see cref="OperationStatus"/> record with
/// <see cref="Status"/> = <c>Queued</c>, returns HTTP 202 Accepted with the
/// operation's location URL, and enqueues the work to a background processor.
///
/// The background processor updates <see cref="Progress"/>, <see cref="Status"/>,
/// and eventually <see cref="ResultData"/> or <see cref="ErrorMessage"/>.
///
/// Clients poll <c>GET /api/v1/operations/{id}</c> to track progress.
/// </summary>
public class OperationStatus
{
    /// <summary>
    /// Unique identifier for this operation.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Discriminator that identifies the type of operation being performed
    /// (e.g. "PatientImport", "ReportGeneration", "BulkUpdate").
    /// </summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>
    /// Current status: Queued, Processing, Completed, Failed.
    /// </summary>
    public string Status { get; set; } = OperationStatusValue.Queued;

    /// <summary>
    /// Progress percentage (0–100).
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// The original request payload as JSON, preserved for audit and retry.
    /// </summary>
    public string? RequestData { get; set; }

    /// <summary>
    /// The operation result as JSON (populated on completion).
    /// </summary>
    public string? ResultData { get; set; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Timestamp when the record was created (when the operation was queued).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the operation completed or failed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// TTL expiration — records are automatically cleaned up 24 hours after creation.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// String constants for <see cref="OperationStatus.Status"/> values.
/// </summary>
public static class OperationStatusValue
{
    public const string Queued = "Queued";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
