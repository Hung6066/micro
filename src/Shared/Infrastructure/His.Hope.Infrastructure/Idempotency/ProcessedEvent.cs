namespace His.Hope.Infrastructure.Idempotency;

/// <summary>
/// Represents a domain event that has already been consumed, ensuring
/// at-most-once delivery semantics in the event bus.
/// Maps to the <c>processed_events</c> table.
/// </summary>
public class ProcessedEvent
{
    public Guid EventId { get; set; }
    public string Consumer { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}
