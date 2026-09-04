namespace His.Hope.Messaging;

using System.Collections.Concurrent;

/// <summary>
/// Small, deterministic schema compatibility seam. Producers can register the
/// highest version they emit and consumers can reject newer versions before
/// executing side effects. Full schema storage remains provider-owned.
/// </summary>
public sealed class EventSchemaRegistry
{
    private readonly ConcurrentDictionary<string, int> _versions = new(StringComparer.Ordinal);

    public void Register(string eventType, int maximumVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumVersion, 1);
        if (_versions.TryAdd(eventType, maximumVersion))
            return;

        if (_versions.TryGetValue(eventType, out var registeredVersion) &&
            registeredVersion != maximumVersion)
        {
            throw new InvalidOperationException(
                $"Event schema '{eventType}' is already registered at version {registeredVersion}; conflicting version {maximumVersion} was rejected.");
        }
    }

    public bool IsCompatible(EventEnvelope envelope) =>
        _versions.TryGetValue(envelope.EventType, out var maximum) && envelope.SchemaVersion <= maximum;

    public bool IsCompatible(string eventType, int schemaVersion) =>
        !string.IsNullOrWhiteSpace(eventType) &&
        schemaVersion >= 1 &&
        _versions.TryGetValue(eventType, out var maximum) &&
        schemaVersion <= maximum;

    public void Validate(string eventType, int schemaVersion)
    {
        if (!IsCompatible(eventType, schemaVersion))
            throw new InvalidOperationException(
                $"Event schema '{eventType}' version {schemaVersion} is not compatible with this consumer.");
    }

    public void Validate(EventEnvelope envelope)
    {
        // The registry, not the transport default, owns the compatibility
        // ceiling for a registered event type.
        envelope.Validate(new EventDeliveryPolicy(int.MaxValue));
        Validate(envelope.EventType, envelope.SchemaVersion);
    }
}
