using System.Collections.Concurrent;
using System.Reflection;
using His.Hope.EventBus.Abstractions;

namespace His.Hope.Infrastructure.Events;

/// <summary>
/// Scans all His.Hope assemblies at startup to build a lookup of IntegrationEvent types.
/// Replaces fragile <c>Type.GetType()</c> calls that fail when the event type resides in
/// a different assembly (e.g., His.Hope.IntegrationEvents).
/// </summary>
public class EventTypeRegistry
{
    private readonly ConcurrentDictionary<string, Type> _types;

    public EventTypeRegistry()
    {
        _types = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);
        ScanAssemblies();
    }

    /// <summary>Looks up an event type by its full name (preferred) or short name.</summary>
    /// <returns>The matching <see cref="Type"/> or <c>null</c> if not found.</returns>
    public Type? Resolve(string eventTypeName)
    {
        if (string.IsNullOrWhiteSpace(eventTypeName))
            return null;

        // Try full name first (what OutboxMessage.Type stores)
        if (_types.TryGetValue(eventTypeName, out var type))
            return type;

        // Fall back to short name for callers that only have the class name
        if (_types.TryGetValue(eventTypeName.Split('.').Last(), out type))
            return type;

        return null;
    }

    private void ScanAssemblies()
    {
        var integrationEventType = typeof(IntegrationEvent);

        var eventTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(asm => asm.FullName is not null &&
                          asm.FullName.Contains("His.Hope", StringComparison.OrdinalIgnoreCase))
            .SelectMany(asm =>
            {
                try
                {
                    return asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Return the types that were successfully loaded
                    return ex.Types.OfType<Type>();
                }
            })
            .Where(t => t is { IsAbstract: false, IsClass: true } &&
                        integrationEventType.IsAssignableFrom(t));

        foreach (var type in eventTypes)
        {
            if (type.FullName is null)
                continue;

            _types.TryAdd(type.FullName, type);

            // Also index by short name, but don't overwrite if collision
            _types.TryAdd(type.Name, type);
        }
    }
}
