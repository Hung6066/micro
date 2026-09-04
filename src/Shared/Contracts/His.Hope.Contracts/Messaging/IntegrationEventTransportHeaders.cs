using System.Text.Json;

namespace His.Hope.Contracts.Messaging;

/// <summary>
/// Canonical transport headers for integration events. The event body remains
/// the versioned contract; these headers make routing and tracing metadata
/// available without deserializing the payload at the broker boundary.
/// </summary>
public static class IntegrationEventTransportHeaders
{
    public const string EventType = "hishop-event-type";
    public const string SchemaVersion = "hishop-schema-version";
    public const string TenantKey = "hishop-tenant-key";
    public const string CorrelationId = "hishop-correlation-id";
    public const string CausationId = "hishop-causation-id";
    public const string Audience = "hishop-audience";
    public const string Priority = "hishop-priority";
    private static readonly string[] AllowedPriorities = ["P0", "P1", "P2", "P3", "P4"];

    public static void Validate(
        IDictionary<string, object>? headers,
        string expectedEventType,
        int expectedSchemaVersion = 1)
    {
        if (headers is null || headers.Count == 0)
            throw new InvalidOperationException("integration_event_transport_headers_missing");

        var eventType = ReadString(headers, EventType);
        if (!string.Equals(eventType, expectedEventType, StringComparison.Ordinal))
            throw new InvalidOperationException("integration_event_transport_event_type_mismatch");

        var schemaVersion = ReadInt32(headers, SchemaVersion);
        if (schemaVersion != expectedSchemaVersion)
            throw new InvalidOperationException("integration_event_transport_schema_version_unsupported");

        if (headers.TryGetValue(Priority, out var priority) &&
            !AllowedPriorities.Contains(ReadStringValue(priority), StringComparer.Ordinal))
            throw new InvalidOperationException("integration_event_transport_priority_invalid");
    }

    public static Dictionary<string, object> Create(
        string eventType,
        string content,
        string? audience = null)
    {
        var headers = new Dictionary<string, object>
        {
            [EventType] = eventType,
            [SchemaVersion] = 1,
        };

        if (!string.IsNullOrWhiteSpace(audience))
            headers[Audience] = audience;

        try
        {
            using var document = JsonDocument.Parse(content);
            AddString(document.RootElement, "TenantKey", TenantKey, headers);
            AddString(document.RootElement, "CorrelationId", CorrelationId, headers);
            AddString(document.RootElement, "CausationId", CausationId, headers);
            AddPriority(document.RootElement, headers);
            if (TryGetPropertyIgnoreCase(document.RootElement, "SchemaVersion", out var schemaVersion) &&
                schemaVersion.TryGetInt32(out var version) && version > 0)
            {
                headers[SchemaVersion] = version;
            }
        }
        catch (JsonException)
        {
            // Preserve existing dispatcher behaviour for malformed legacy
            // payloads; validation and DLQ handling remain the consumer's job.
        }

        return headers;
    }

    private static void AddString(
        JsonElement root,
        string propertyName,
        string headerName,
        IDictionary<string, object> headers)
    {
        if (TryGetPropertyIgnoreCase(root, propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(property.GetString()))
        {
            headers[headerName] = property.GetString()!;
        }
    }

    private static void AddPriority(JsonElement root, IDictionary<string, object> headers)
    {
        if (!TryGetPropertyIgnoreCase(root, "Priority", out var property) ||
            property.ValueKind != JsonValueKind.String)
            return;

        var priority = property.GetString();
        if (priority is not null && AllowedPriorities.Contains(priority, StringComparer.Ordinal))
            headers[Priority] = priority;
    }
    private static bool TryGetPropertyIgnoreCase(
        JsonElement root,
        string propertyName,
        out JsonElement property)
    {
        foreach (var candidate in root.EnumerateObject())
        {
            if (candidate.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    private static string? ReadString(IDictionary<string, object> headers, string name)
    {
        if (!headers.TryGetValue(name, out var value))
            return null;
        return ReadStringValue(value);
    }

    private static string? ReadStringValue(object? value) => value switch
    {
        string text => text,
        byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
        null => null,
        _ => value.ToString()
    };

    private static int? ReadInt32(IDictionary<string, object> headers, string name)
    {
        if (!headers.TryGetValue(name, out var value))
            return null;
        if (value is int number)
            return number;
        if (value is byte[] bytes && int.TryParse(System.Text.Encoding.UTF8.GetString(bytes), out number))
            return number;
        return int.TryParse(value.ToString(), out number) ? number : null;
    }
}
