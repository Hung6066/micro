namespace His.Hope.Infrastructure.AsyncOperations;

/// <summary>
/// A unit of work enqueued to the background channel for processing.
/// </summary>
public class AsyncOperationWorkItem
{
    /// <summary>
    /// The <see cref="OperationStatus.Id"/> for tracking and progress updates.
    /// </summary>
    public Guid OperationId { get; init; }

    /// <summary>
    /// The discriminator used to look up the handler.
    /// </summary>
    public string OperationType { get; init; } = string.Empty;

    /// <summary>
    /// The deserialized request object to pass to the handler.
    /// </summary>
    public object? Request { get; init; }

    /// <summary>
    /// The raw request data as JSON (for audit/storage).
    /// </summary>
    public string? RequestData { get; init; }
}
