using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SystemDashboard.Bff.Services;

public sealed class ConsulDiscoveryService : IConsulDiscoveryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ConsulDiscoveryService> _logger;

    public ConsulDiscoveryService(HttpClient httpClient, ILogger<ConsulDiscoveryService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<string>> GetServiceNamesAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<Dictionary<string, string[]>>(
                "/v1/catalog/services", ct);

            return response?.Keys
                .Where(k => !string.Equals(k, "consul", StringComparison.OrdinalIgnoreCase))
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch service names from Consul");
            return [];
        }
    }

    public async Task<ConsulServiceHealth?> GetServiceHealthAsync(string serviceName, CancellationToken ct = default)
    {
        try
        {
            var entries = await _httpClient.GetFromJsonAsync<ConsulServiceEntry[]>(
                $"/v1/health/service/{serviceName}", ct);

            if (entries is null || entries.Length == 0)
                return null;

            var first = entries[0];
            var checks = first.Checks?.Select(c => new ConsulHealthCheck
            {
                Name = c.Name ?? "",
                Status = c.Status ?? "unknown",
                Output = c.Output
            }).ToList() ?? [];

            var status = AggregateStatus(checks);

            return new ConsulServiceHealth
            {
                ServiceName = serviceName,
                Status = status,
                Port = first.Service?.Port ?? 0,
                Address = first.Service?.Address,
                Checks = checks
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch health for service {ServiceName}", serviceName);
            return null;
        }
    }

    private static string AggregateStatus(List<ConsulHealthCheck> checks)
    {
        if (checks.Count == 0)
            return "unknown";

        if (checks.All(c => c.Status == "passing"))
            return "passing";

        if (checks.Any(c => c.Status == "critical"))
            return "critical";

        return "warning";
    }

    // Private nested records for Consul JSON deserialization
    private sealed record ConsulServiceEntry
    {
        public ConsulServiceDetail? Service { get; init; }
        public ConsulCheckEntry[]? Checks { get; init; }
    }

    private sealed record ConsulServiceDetail
    {
        public int Port { get; init; }
        public string? Address { get; init; }
    }

    private sealed record ConsulCheckEntry
    {
        [JsonPropertyName("Name")]
        public string? Name { get; init; }

        [JsonPropertyName("Status")]
        public string? Status { get; init; }

        [JsonPropertyName("Output")]
        public string? Output { get; init; }
    }
}
