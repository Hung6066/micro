using Microsoft.Extensions.Configuration;

namespace His.Hope.Observability.OpenTelemetry;

internal sealed record TracingConfiguration(Uri? Endpoint, string ServiceName, string EnvironmentName, IReadOnlyDictionary<string, object> Attributes)
{
    internal static TracingConfiguration Read(IConfiguration configuration, string serviceName)
    {
        var endpointKey = new[] { "OTEL_EXPORTER_OTLP_ENDPOINT", "OpenTelemetry:OtlpEndpoint", "Otlp:Endpoint" }
            .FirstOrDefault(key => configuration[key] is not null);
        Uri? endpoint = null;
        if (endpointKey is not null &&
            (!Uri.TryCreate(configuration[endpointKey], UriKind.Absolute, out endpoint) ||
             (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps) ||
             string.IsNullOrEmpty(endpoint.Host) || !string.IsNullOrEmpty(endpoint.UserInfo) ||
             !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment)))
        {
            throw new InvalidOperationException($"{endpointKey} must be an absolute HTTP(S) OTLP/gRPC endpoint without credentials, query or fragment.");
        }

        var attributes = new Dictionary<string, object>(StringComparer.Ordinal);
        var resourceAttributes = configuration["OTEL_RESOURCE_ATTRIBUTES"];
        if (!string.IsNullOrWhiteSpace(resourceAttributes))
        {
            foreach (var entry in resourceAttributes.Split(','))
            {
                var separator = entry.IndexOf('=');
                if (separator <= 0 || string.IsNullOrWhiteSpace(entry[(separator + 1)..]))
                    throw new InvalidOperationException("OTEL_RESOURCE_ATTRIBUTES must contain comma-separated key=value pairs.");

                var key = entry[..separator].Trim();
                if (string.IsNullOrEmpty(key))
                    throw new InvalidOperationException("OTEL_RESOURCE_ATTRIBUTES contains an empty key.");

                // Project/tenant attribution is authoritative only at the collector.
                if (key.Contains("tenant", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("project", StringComparison.OrdinalIgnoreCase))
                    continue;

                attributes[key] = Uri.UnescapeDataString(entry[(separator + 1)..].Trim());
            }
        }

        var name = configuration["OTEL_SERVICE_NAME"] ?? serviceName;
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("The OpenTelemetry service name must not be empty.");

        var environment = configuration["ASPNETCORE_ENVIRONMENT"]
            ?? configuration["DOTNET_ENVIRONMENT"]
            ?? configuration["Environment"]
            ?? attributes.GetValueOrDefault("deployment.environment.name") as string
            ?? "development";
        if (string.IsNullOrWhiteSpace(environment))
            throw new InvalidOperationException("The OpenTelemetry deployment environment must not be empty.");

        return new TracingConfiguration(endpoint, name, environment, attributes);
    }
}
