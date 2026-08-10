using System.Text.Json;

namespace His.Hope.Messaging;

public sealed record EventEnvelope(
    Guid Id,
    string EventType,
    string Payload,
    DateTimeOffset OccurredAt,
    string? CorrelationId = null,
    string? CausationId = null,
    int SchemaVersion = 1,
    IReadOnlyDictionary<string, string>? Headers = null)
{
    public const int CurrentSchemaVersion = 1;

    public static EventEnvelope Create<T>(T payload, string? eventType = null, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new(
            Guid.NewGuid(),
            eventType ?? typeof(T).FullName ?? typeof(T).Name,
            JsonSerializer.Serialize(payload, options),
            DateTimeOffset.UtcNow);
    }

    public T Deserialize<T>(JsonSerializerOptions? options = null) =>
        JsonSerializer.Deserialize<T>(Payload, options) ?? throw new JsonException("Event payload was empty.");

    public EventEnvelope Validate(EventDeliveryPolicy? policy = null)
    {
        policy ??= EventDeliveryPolicy.Default;
        if (Id == Guid.Empty) throw new ArgumentException("Event id is required.", nameof(Id));
        if (string.IsNullOrWhiteSpace(EventType)) throw new ArgumentException("Event type is required.", nameof(EventType));
        if (EventType.Length > policy.MaximumEventTypeLength)
            throw new ArgumentException("Event type exceeds the configured limit.", nameof(EventType));
        if (string.IsNullOrWhiteSpace(Payload)) throw new ArgumentException("Event payload is required.", nameof(Payload));
        if (OccurredAt > DateTimeOffset.UtcNow.AddMinutes(5)) throw new ArgumentException("Event occurred-at cannot be in the future.", nameof(OccurredAt));
        if (SchemaVersion < 1 || SchemaVersion > policy.MaximumSchemaVersion)
            throw new ArgumentOutOfRangeException(nameof(SchemaVersion), SchemaVersion, "Unsupported event schema version.");
        if (System.Text.Encoding.UTF8.GetByteCount(Payload) > policy.MaximumPayloadBytes)
            throw new ArgumentException("Event payload exceeds the configured limit.", nameof(Payload));
        return this;
    }
}
