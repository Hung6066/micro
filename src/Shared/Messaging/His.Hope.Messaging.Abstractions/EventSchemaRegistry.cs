namespace His.Hope.Messaging;

/// <summary>
/// Small, deterministic schema compatibility seam. Producers can register the
/// highest version they emit and consumers can reject newer versions before
/// executing side effects. Full schema storage remains provider-owned.
/// </summary>
public sealed class EventSchemaRegistry
{
    private readonly Dictionary<string, int> _versions = new(StringComparer.Ordinal);

    public void Register(string eventType, int maximumVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        if (maximumVersion < 1) throw new ArgumentOutOfRangeException(nameof(maximumVersion));
        _versions[eventType] = maximumVersion;
    }

    public bool IsCompatible(EventEnvelope envelope) =>
        _versions.TryGetValue(envelope.EventType, out var maximum) && envelope.SchemaVersion <= maximum;

    public void Validate(EventEnvelope envelope)
    {
        // The registry, not the transport default, owns the compatibility
        // ceiling for a registered event type.
        envelope.Validate(new EventDeliveryPolicy(int.MaxValue));
        if (!IsCompatible(envelope))
            throw new InvalidOperationException($"Event schema '{envelope.EventType}' version {envelope.SchemaVersion} is not compatible with this consumer.");
    }
}
