using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;

namespace SystemDashboard.Bff.Aggregators;

public sealed class ResourceAggregator : IResourceAggregator
{
    /// <summary>
    /// Maps service names to Docker Compose service hostnames.
    /// Used for direct health checks when running inside a container.
    /// </summary>
    private static readonly Dictionary<string, string> _dockerHostnames = new()
    {
        ["identity-service"] = "identityservice",
        ["patient-service"] = "patientservice",
        ["appointment-service"] = "appointmentservice",
        ["clinical-service"] = "clinicalservice",
        ["lab-service"] = "labservice",
        ["billing-service"] = "billingservice",
        ["pharmacy-service"] = "pharmacyservice",
    };

    private readonly IConsulDiscoveryService _consul;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPrometheusQueryService _prometheus;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ResourceAggregator> _logger;
    private readonly bool _runningInContainer;

    private static readonly Dictionary<string, string> ServiceToJobMap = new()
    {
        ["identity-service"] = "identityservice",
        ["patient-service"] = "patientservice",
        ["appointment-service"] = "appointmentservice",
        ["clinical-service"] = "clinicalservice",
        ["lab-service"] = "labservice",
        ["billing-service"] = "billingservice",
        ["pharmacy-service"] = "pharmacyservice",
    };

    private static readonly string CpuPromqlTemplate = "rate(process_cpu_time_seconds_total{job=\"{job}\"}[5m]) * 100";
    private static readonly string MemoryPromqlTemplate = "process_memory_usage_bytes{job=\"{job}\"} / 1024 / 1024";

    private static readonly Dictionary<string, (int httpPort, int? grpcPort, string type, string[] databases)> _serviceMap = new()
    {
        ["identity-service"] = (5001, 5007, "service", new[] { "identitydb" }),
        ["patient-service"] = (5002, 5006, "service", new[] { "patientdb" }),
        ["appointment-service"] = (5003, 5007, "service", new[] { "appointmentdb" }),
        ["clinical-service"] = (5004, null, "service", new[] { "clinicaldb" }),
        ["lab-service"] = (5010, null, "service", new[] { "labdb" }),
        ["billing-service"] = (5020, null, "service", new[] { "billingdb" }),
        ["pharmacy-service"] = (5030, null, "service", new[] { "pharmacydb" }),
        ["patient-bff"] = (5100, null, "bff", Array.Empty<string>()),
        ["clinical-bff"] = (5200, null, "bff", Array.Empty<string>()),
        ["lab-bff"] = (5300, null, "bff", Array.Empty<string>()),
        ["billing-bff"] = (5400, null, "bff", Array.Empty<string>()),
        ["pharmacy-bff"] = (5500, null, "bff", Array.Empty<string>()),
        ["dashboard-bff"] = (5700, null, "bff", Array.Empty<string>()),
    };

    private static readonly InfrastructureResource[] _infraResources =
    [
        new() { Name = "Redis", DisplayName = "Redis", Status = "Running", HealthStatus = "Healthy", Type = "infrastructure", Category = "Cache", Version = "7.2" },
        new() { Name = "RabbitMQ", DisplayName = "RabbitMQ", Status = "Running", HealthStatus = "Healthy", Type = "infrastructure", Category = "Message Queue", Version = "3.13" },
        new() { Name = "Elasticsearch", DisplayName = "Elasticsearch", Status = "Running", HealthStatus = "Healthy", Type = "infrastructure", Category = "Search", Version = "8.12" },
        new() { Name = "API Gateway", DisplayName = "API Gateway", Status = "Running", HealthStatus = "Healthy", Type = "infrastructure", Category = "Gateway", Version = "YARP 2.1" },
    ];

    private static readonly DatabaseResource[] _databaseResources =
    [
        new() { Name = "identitydb", DisplayName = "Identity DB", Status = "Running", HealthStatus = "Healthy", Type = "database", Engine = "CockroachDB" },
        new() { Name = "patientdb", DisplayName = "Patient DB", Status = "Running", HealthStatus = "Healthy", Type = "database", Engine = "CockroachDB" },
        new() { Name = "appointmentdb", DisplayName = "Appointment DB", Status = "Running", HealthStatus = "Healthy", Type = "database", Engine = "CockroachDB" },
        new() { Name = "clinicaldb", DisplayName = "Clinical DB", Status = "Running", HealthStatus = "Healthy", Type = "database", Engine = "CockroachDB" },
        new() { Name = "labdb", DisplayName = "Lab DB", Status = "Running", HealthStatus = "Healthy", Type = "database", Engine = "CockroachDB" },
        new() { Name = "billingdb", DisplayName = "Billing DB", Status = "Running", HealthStatus = "Healthy", Type = "database", Engine = "CockroachDB" },
        new() { Name = "pharmacydb", DisplayName = "Pharmacy DB", Status = "Running", HealthStatus = "Healthy", Type = "database", Engine = "CockroachDB" },
        new() { Name = "harnessdb", DisplayName = "Harness DB", Status = "Running", HealthStatus = "Healthy", Type = "database", Engine = "CockroachDB" },
    ];

    public ResourceAggregator(
        IConsulDiscoveryService consul,
        IHttpClientFactory httpClientFactory,
        IPrometheusQueryService prometheus,
        IMemoryCache cache,
        ILogger<ResourceAggregator> logger)
    {
        _consul = consul;
        _httpClientFactory = httpClientFactory;
        _prometheus = prometheus;
        _cache = cache;
        _logger = logger;
        _runningInContainer = string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<Resource>> GetAllResourcesAsync(CancellationToken ct = default)
    {
        var result = await _cache.GetOrCreateAsync(CacheKeys.AllResources, async () =>
        {
            // Fetch Consul services (eager — needed for health lookup)
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

            // Phase 1: Launch all queries simultaneously
            var healthTasks = new Dictionary<string, Task<ConsulServiceHealth?>>();
            var cpuTasks = new Dictionary<string, Task<double?>>();
            var memoryTasks = new Dictionary<string, Task<double?>>();

            foreach (var (name, _) in _serviceMap)
            {
                healthTasks[name] = GetHealthSafeAsync(name, ct);
                if (ServiceToJobMap.TryGetValue(name, out var job))
                {
                    cpuTasks[name] = QueryLatestMetricValueAsync(
                        CpuPromqlTemplate.Replace("{job}", job), ct);
                    memoryTasks[name] = QueryLatestMetricValueAsync(
                        MemoryPromqlTemplate.Replace("{job}", job), ct);
                }
            }

            // Await all at once
            var allTasks = healthTasks.Values
                .Concat<object>(cpuTasks.Values)
                .Concat<object>(memoryTasks.Values)
                .Cast<Task>();
            await Task.WhenAll(allTasks);

            // Phase 2: Assemble results
            var resources = new List<Resource>();
            foreach (var (name, (httpPort, grpcPort, _, databases)) in _serviceMap)
            {
                var consulHealth = healthTasks.TryGetValue(name, out var hTask)
                    ? (hTask.IsCompletedSuccessfully ? hTask.Result : null)
                    : null;

                var (stateStr, healthStr, checks) = consulHealth is not null
                    ? MapFromConsul(consulHealth)
                    : await CheckDirectHealthAsync(name, httpPort, ct);

                double? cpuPercent = cpuTasks.TryGetValue(name, out var cTask)
                    && cTask.IsCompletedSuccessfully ? cTask.Result : null;
                double? memoryMb = memoryTasks.TryGetValue(name, out var mTask)
                    && mTask.IsCompletedSuccessfully ? mTask.Result : null;

                resources.Add(new ServiceResource
                {
                    Name = name,
                    DisplayName = FormatServiceName(name),
                    Status = stateStr,
                    HealthStatus = healthStr,
                    Type = "service",
                    HealthChecks = checks,
                    HttpPort = httpPort,
                    GrpcPort = grpcPort,
                    CpuPercent = cpuPercent,
                    MemoryUsedMb = memoryMb,
                    Databases = databases.ToList(),
                });
            }

            resources.AddRange(_infraResources);
            resources.AddRange(_databaseResources);
            return resources;
        }, TimeSpan.FromSeconds(15));
        return result!;
    }

    public async Task<Resource?> GetResourceByNameAsync(string name, CancellationToken ct = default)
    {
        var resources = await GetAllResourcesAsync(ct);
        return resources.FirstOrDefault(r =>
            string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Maps Consul service health data to status strings and health checks.
    /// </summary>
    private static (string stateStr, string healthStr, List<HealthCheckResult> checks) MapFromConsul(
        ConsulServiceHealth health)
    {
        var state = health.Status switch
        {
            "passing" => ServiceState.Running,
            "critical" => ServiceState.Stopped,
            "warning" => ServiceState.Degraded,
            _ => ServiceState.Unknown
        };
        var stateStr = state switch
        {
            ServiceState.Running => "Running",
            ServiceState.Stopped => "Stopped",
            ServiceState.Degraded => "Degraded",
            _ => "Unknown"
        };
        var healthStr = state switch
        {
            ServiceState.Running => "Healthy",
            ServiceState.Stopped => "Unhealthy",
            ServiceState.Degraded => "Degraded",
            _ => "Unknown"
        };
        var checks = health.Checks
            .Select(c => new HealthCheckResult
            {
                Name = c.Name,
                Status = c.Status,
                Output = c.Output
            })
            .ToList();
        return (stateStr, healthStr, checks);
    }

    /// <summary>
    /// Falls back to a direct HTTP health check when Consul has no data for a service.
    /// Tries GET http://localhost:{port}/health (or http://{dockerHost}:{port}/health in container)
    /// with a 3-second timeout.
    /// </summary>
    private async Task<(string stateStr, string healthStr, List<HealthCheckResult> checks)>
        CheckDirectHealthAsync(string serviceName, int port, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            // Determine host: use Docker service name in container, localhost otherwise
            var host = _runningInContainer && _dockerHostnames.TryGetValue(serviceName, out var dockerHost)
                ? dockerHost
                : "localhost";

            var client = _httpClientFactory.CreateClient("health-check");
            var url = $"http://{host}:{port}/health";
            var response = await client.GetAsync(url, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Health check for {Service} returned {Status}", serviceName, response.StatusCode);
                return ("Stopped", "Unhealthy", []);
            }

            var body = await response.Content.ReadAsStringAsync(cts.Token);

            // Some services return plain text "Healthy", others return JSON
            bool isHealthy = body.Trim().Equals("Healthy", StringComparison.OrdinalIgnoreCase);

            if (!isHealthy)
            {
                try
                {
                    var healthDoc = JsonSerializer.Deserialize<HealthJsonResponse>(body, _jsonOptions);
                    isHealthy = healthDoc?.Status?.Equals("Healthy", StringComparison.OrdinalIgnoreCase) == true;
                }
                catch (JsonException)
                {
                    // Not JSON, use the plain text result
                }
            }

            if (isHealthy)
            {
                return ("Running", "Healthy",
                [
                    new HealthCheckResult { Name = "direct-http", Status = "passing", Output = "Direct health check passed" }
                ]);
            }

            return ("Unknown", "Unknown", []);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Health check for {Service} timed out on port {Port}", serviceName, port);
            return ("Stopped", "Unhealthy", []);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Health check for {Service} failed on port {Port}", serviceName, port);
            return ("Stopped", "Unhealthy", []);
        }
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed record HealthJsonResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }
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

    private async Task<double?> QueryLatestMetricValueAsync(string promql, CancellationToken ct)
    {
        try
        {
            var point = await _prometheus.QueryAsync(promql, ct);
            return point?.Value;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to query Prometheus for metric: {Query}", promql);
            return null;
        }
    }

    private async Task<ConsulServiceHealth?> GetHealthSafeAsync(
        string serviceName, CancellationToken ct)
    {
        try
        {
            return await _consul.GetServiceHealthAsync(serviceName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Consul health check failed for {Service}", serviceName);
            return null;
        }
    }
}
