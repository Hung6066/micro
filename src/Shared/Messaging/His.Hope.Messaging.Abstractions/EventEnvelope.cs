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
}
