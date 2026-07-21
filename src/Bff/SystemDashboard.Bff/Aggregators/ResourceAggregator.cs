using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;

namespace SystemDashboard.Bff.Aggregators;

public sealed class ResourceAggregator : IResourceAggregator
{
    private readonly IConsulDiscoveryService _consul;
    private readonly ILogger<ResourceAggregator> _logger;

    private static readonly Dictionary<string, (int httpPort, int? grpcPort, string type, string[] databases)> _serviceMap = new()
    {
        ["identity-service"] = (5001, 5007, "service", new[] { "identitydb" }),
        ["patient-service"] = (5002, 5006, "service", new[] { "patientdb" }),
        ["appointment-service"] = (5004, 5008, "service", new[] { "appointmentdb" }),
        ["clinical-service"] = (5005, 5009, "service", new[] { "clinicaldb" }),
        ["lab-service"] = (5010, null, "service", new[] { "labdb" }),
        ["billing-service"] = (5020, null, "service", new[] { "billingdb" }),
        ["pharmacy-service"] = (5030, null, "service", new[] { "pharmacydb" }),
        ["patient-bff"] = (5100, null, "bff", Array.Empty<string>()),
        ["clinical-bff"] = (5200, null, "bff", Array.Empty<string>()),
        ["lab-bff"] = (5300, null, "bff", Array.Empty<string>()),
        ["billing-bff"] = (5400, null, "bff", Array.Empty<string>()),
        ["pharmacy-bff"] = (5500, null, "bff", Array.Empty<string>()),
        ["dashboard-bff"] = (5600, null, "bff", Array.Empty<string>()),
    };

    private static readonly InfrastructureResource[] _infraResources =
    [
        new() { Name = "Redis", Category = "Cache", Version = "7.2" },
        new() { Name = "RabbitMQ", Category = "Message Queue", Version = "3.13" },
        new() { Name = "Elasticsearch", Category = "Search", Version = "8.12" },
        new() { Name = "API Gateway", Category = "Gateway", Version = "YARP 2.1" },
    ];

    private static readonly DatabaseResource[] _databaseResources =
    [
        new() { Name = "identitydb", Engine = "CockroachDB" },
        new() { Name = "patientdb", Engine = "CockroachDB" },
        new() { Name = "appointmentdb", Engine = "CockroachDB" },
        new() { Name = "clinicaldb", Engine = "CockroachDB" },
        new() { Name = "labdb", Engine = "CockroachDB" },
        new() { Name = "billingdb", Engine = "CockroachDB" },
        new() { Name = "pharmacydb", Engine = "CockroachDB" },
        new() { Name = "harnessdb", Engine = "CockroachDB" },
    ];

    public ResourceAggregator(IConsulDiscoveryService consul, ILogger<ResourceAggregator> logger)
    {
        _consul = consul;
        _logger = logger;
    }

    public async Task<List<Resource>> GetAllResourcesAsync(CancellationToken ct = default)
    {
        var resources = new List<Resource>();

        // Fetch service health from Consul
        List<string> consulServices;
        try
        {
            consulServices = await _consul.GetServiceNamesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get service names from Consul");
            consulServices = [];
        }

        // Build service resources from the map
        foreach (var (name, (httpPort, grpcPort, _, databases)) in _serviceMap)
        {
            var health = consulServices.Contains(name)
                ? await _consul.GetServiceHealthAsync(name, ct)
                : null;

            var state = MapServiceState(health?.Status);
            var checks = health?.Checks
                .Select(c => new HealthCheckResult
                {
                    Name = c.Name,
                    Status = c.Status,
                    Output = c.Output
                })
                .ToList() ?? [];

            resources.Add(new ServiceResource
            {
                Name = name,
                DisplayName = FormatServiceName(name),
                HttpPort = httpPort,
                GrpcPort = grpcPort,
                HealthStatus = state.ToString(),
                Databases = databases.ToList(),
                HealthChecks = checks
            });
        }

        // Append hardcoded infrastructure resources
        resources.AddRange(_infraResources);

        // Append hardcoded database resources
        resources.AddRange(_databaseResources);

        return resources;
    }

    public async Task<Resource?> GetResourceByNameAsync(string name, CancellationToken ct = default)
    {
        var resources = await GetAllResourcesAsync(ct);
        return resources.FirstOrDefault(r =>
            string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static ServiceState MapServiceState(string? status) => status switch
    {
        "passing" => ServiceState.Running,
        "critical" => ServiceState.Stopped,
        "warning" => ServiceState.Degraded,
        _ => ServiceState.Unknown
    };

    /// <summary>
    /// Converts a kebab-case service name to a human-readable display name.
    /// Example: "patient-service" → "Patient Service"
    /// </summary>
    private static string FormatServiceName(string name)
    {
        return string.Join(' ', name.Split('-')
            .Select(word => word.Length > 0
                ? char.ToUpperInvariant(word[0]) + word[1..]
                : word));
    }
}
