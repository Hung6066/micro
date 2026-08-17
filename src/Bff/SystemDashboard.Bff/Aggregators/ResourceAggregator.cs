using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using His.Hope.Configuration;
using SystemDashboard.Bff.Models;
using SystemDashboard.Bff.Services;

namespace SystemDashboard.Bff.Aggregators;

public sealed class ResourceAggregator : IResourceAggregator
{
    private readonly IConsulDiscoveryService _consul;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPrometheusQueryService _prometheus;
    private readonly IKubernetesPodMetricsService _kubernetesMetrics;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ResourceAggregator> _logger;
    private readonly ServiceEndpointOptions _runtimeEndpoints;
    private readonly bool _runningInKubernetes;

    private static readonly string CpuPromqlTemplate = "rate(process_cpu_seconds_total{service=\"{service}\"}[5m]) * 100";
    private static readonly string MemoryPromqlTemplate = "process_resident_memory_bytes{service=\"{service}\"} / 1024 / 1024";

    private static readonly Dictionary<string, ServiceRuntimeRegistration> _serviceMap = new()
    {
        ["identity-service"] = new("identity-api", "identity-grpc", new[] { "identitydb" }),
        ["patient-service"] = new("patient-api", "patient-grpc", new[] { "patientdb" }),
        ["appointment-service"] = new("appointment-api", "appointment-grpc", new[] { "appointmentdb" }),
        ["clinical-service"] = new("clinical-api", "clinical-grpc", new[] { "clinicaldb" }),
        ["lab-service"] = new("lab-api", "lab-grpc", new[] { "labdb" }),
        ["billing-service"] = new("billing-api", "billing-grpc", new[] { "billingdb" }),
        ["pharmacy-service"] = new("pharmacy-api", "pharmacy-grpc", new[] { "pharmacydb" }),
        ["patient-bff"] = new("patient-bff", null, Array.Empty<string>()),
        ["clinical-bff"] = new("clinical-bff", null, Array.Empty<string>()),
        ["lab-bff"] = new("lab-bff", null, Array.Empty<string>()),
        ["billing-bff"] = new("billing-bff", null, Array.Empty<string>()),
        ["pharmacy-bff"] = new("pharmacy-bff", null, Array.Empty<string>()),
        ["dashboard-bff"] = new("dashboard-bff", null, Array.Empty<string>()),
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
        IKubernetesPodMetricsService kubernetesMetrics,
        IOptions<KubernetesOptions> kubernetesOptions,
        IMemoryCache cache,
        ILogger<ResourceAggregator> logger,
        ServiceEndpointOptions runtimeEndpoints)
    {
        _consul = consul;
        _httpClientFactory = httpClientFactory;
        _prometheus = prometheus;
        _kubernetesMetrics = kubernetesMetrics;
        _cache = cache;
        _logger = logger;
        _runtimeEndpoints = runtimeEndpoints;
        _runningInKubernetes = kubernetesOptions.Value.Enabled || !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));
        _logger.LogInformation("Resource metrics source selected: KubernetesMetricsApi={Enabled}", _runningInKubernetes);
    }

    public async Task<List<Resource>> GetAllResourcesAsync(CancellationToken ct = default)
    {
        var result = await _cache.GetOrCreateAsync(CacheKeys.AllResources, async () =>
        {
            // Fetch Consul services (eager — needed for health lookup)
            List<string> consulServices;
            if (_runningInKubernetes)
            {
                // K3s uses Kubernetes service DNS and does not require Consul
                // for liveness. Avoid making the health page depend on an
                // optional Consul deployment.
                consulServices = [];
            }
            else
            {
                try
                {
                    consulServices = await _consul.GetServiceNamesAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get service names from Consul");
                    consulServices = [];
                }
            }

            // Phase 1: Launch all queries simultaneously
            var healthTasks = new Dictionary<string, Task<ConsulServiceHealth?>>();
            var directHealthTasks = new Dictionary<string, Task<(string stateStr, string healthStr, List<HealthCheckResult> checks)>>();
            var cpuTasks = new Dictionary<string, Task<double?>>();
            var memoryTasks = new Dictionary<string, Task<double?>>();
            var k8sMetrics = _runningInKubernetes
                ? await _kubernetesMetrics.GetServiceMetricsAsync(_serviceMap.Keys, ct)
                : new Dictionary<string, KubernetesServiceMetrics>();

            foreach (var name in _serviceMap.Keys)
            {
                healthTasks[name] = _runningInKubernetes
                    ? Task.FromResult<ConsulServiceHealth?>(null)
                    : GetHealthSafeAsync(name, ct);
                if (_runningInKubernetes)
                {
                    // Do not serialize the 3-second health-check timeout for
                    // every service; one slow pod must not make /api/resources
                    // exceed the dashboard request timeout.
                    directHealthTasks[name] = CheckDirectHealthAsync(
                        _serviceMap[name].HttpEndpointKey, ct);
                }
                if (_serviceMap.ContainsKey(name))
                {
                    if (k8sMetrics.TryGetValue(name, out var metrics))
                    {
                        cpuTasks[name] = Task.FromResult<double?>(metrics.CpuPercent);
                        memoryTasks[name] = Task.FromResult<double?>(metrics.MemoryUsedMb);
                    }
                    else
                    {
                        cpuTasks[name] = QueryLatestMetricValueAsync(
                            CpuPromqlTemplate.Replace("{service}", name), ct);
                        memoryTasks[name] = QueryLatestMetricValueAsync(
                            MemoryPromqlTemplate.Replace("{service}", name), ct);
                    }
                }
            }

            // Await all at once
            var allTasks = healthTasks.Values
                .Concat<object>(directHealthTasks.Values)
                .Concat<object>(cpuTasks.Values)
                .Concat<object>(memoryTasks.Values)
                .Cast<Task>();
            await Task.WhenAll(allTasks);

            // Phase 2: Assemble results
            var resources = new List<Resource>();
            foreach (var (name, registration) in _serviceMap)
            {
                var consulHealth = healthTasks.TryGetValue(name, out var hTask)
                    ? (hTask.IsCompletedSuccessfully ? hTask.Result : null)
                    : null;

                var (stateStr, healthStr, checks) = consulHealth is not null
                    ? MapFromConsul(consulHealth)
                    : directHealthTasks.TryGetValue(name, out var directHealthTask)
                        ? await directHealthTask
                        : await CheckDirectHealthAsync(registration.HttpEndpointKey, ct);

                double? cpuPercent = cpuTasks.TryGetValue(name, out var cTask)
                    && cTask.IsCompletedSuccessfully ? cTask.Result : null;
                double? memoryMb = memoryTasks.TryGetValue(name, out var mTask)
                    && mTask.IsCompletedSuccessfully ? mTask.Result : null;
                var httpPort = _runtimeEndpoints.GetRequired(registration.HttpEndpointKey).Port;
                int? grpcPort = registration.GrpcEndpointKey is null
                    ? null
                    : _runtimeEndpoints.GetRequired(registration.GrpcEndpointKey).Port;

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
                    Databases = registration.Databases.ToList(),
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
    /// Tries GET {serviceBaseUrl}/health with a 3-second timeout.
    /// </summary>
    private async Task<(string stateStr, string healthStr, List<HealthCheckResult> checks)>
        CheckDirectHealthAsync(string endpointKey, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var client = _httpClientFactory.CreateClient("health-check");
            var healthUrl = new Uri(_runtimeEndpoints.GetRequired(endpointKey), "health");
            var response = await client.GetAsync(healthUrl, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Health check for {EndpointKey} returned {Status}", endpointKey, response.StatusCode);
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
            _logger.LogDebug("Health check for {EndpointKey} timed out", endpointKey);
            return ("Stopped", "Unhealthy", []);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Health check for {EndpointKey} failed", endpointKey);
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
            // Metrics are supplementary to service liveness. A missing or
            // restarting Prometheus must not block /api/resources.
            using var metricsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            metricsCts.CancelAfter(TimeSpan.FromMilliseconds(750));
            var point = await _prometheus.QueryAsync(promql, metricsCts.Token);
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

    private sealed record ServiceRuntimeRegistration(
        string HttpEndpointKey,
        string? GrpcEndpointKey,
        string[] Databases);
}
