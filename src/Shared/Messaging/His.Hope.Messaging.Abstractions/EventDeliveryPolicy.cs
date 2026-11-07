namespace His.Hope.Messaging;

/// <summary>
/// Shared reliability policy for events crossing a service seam.
/// The policy is deliberately transport-neutral so RabbitMQ, test doubles and
/// future providers enforce the same invariants.
/// </summary>
public sealed record EventDeliveryPolicy(
    int MaximumSchemaVersion = EventEnvelope.CurrentSchemaVersion,
    int MaximumEventTypeLength = 255,
    int MaximumPayloadBytes = 4 * 1024 * 1024)
{
    public static EventDeliveryPolicy Default { get; } = new();

    public static bool IsAllowedPriority(string? priority) =>
        priority is "P0" or "P1" or "P2" or "P3" or "P4";

    public void Validate(EventEnvelope envelope)
    {
        envelope.Validate(this);
    }
}

public static class EventEnvelopeHeaders
{
    public const string SchemaVersion = "hishop-schema-version";
    public const string CorrelationId = "hishop-correlation-id";
    public const string CausationId = "hishop-causation-id";
    public const string Priority = "hishop-priority";
    public const string PartitionKey = "hishop-partition-key";
}
