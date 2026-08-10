using System.Collections.ObjectModel;

namespace His.Hope.Configuration;

public sealed class ServiceEndpointOptions
{
    private readonly Dictionary<string, ServiceEndpointRegistration> _endpoints =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, ServiceEndpointRegistration> Endpoints =>
        new ReadOnlyDictionary<string, ServiceEndpointRegistration>(_endpoints);

    public Uri GetRequired(string logicalName)
    {
        var normalizedName = NormalizeLogicalName(logicalName);
        if (!_endpoints.TryGetValue(normalizedName, out var endpoint) || endpoint.Uri is null)
        {
            throw new InvalidOperationException(
                $"Runtime endpoint '{normalizedName}' is required but was not configured.");
        }

        return endpoint.Uri;
    }

    public Uri? GetOptional(string logicalName)
    {
        var normalizedName = NormalizeLogicalName(logicalName);
        return _endpoints.TryGetValue(normalizedName, out var endpoint)
            ? endpoint.Uri
            : null;
    }

    public bool TryGet(string logicalName, out Uri? uri)
    {
        uri = GetOptional(logicalName);
        return uri is not null;
    }

    internal void Set(string logicalName, Uri? uri, bool required, string sourceKey)
    {
        var normalizedName = NormalizeLogicalName(logicalName);
        _endpoints[normalizedName] = new ServiceEndpointRegistration(
            normalizedName,
            uri,
            required,
            sourceKey);
    }

    internal static string NormalizeLogicalName(string logicalName) =>
        logicalName.Trim().Replace('_', '-').ToLowerInvariant();
}

public sealed record ServiceEndpointRegistration(
    string LogicalName,
    Uri? Uri,
    bool Required,
    string SourceKey);
