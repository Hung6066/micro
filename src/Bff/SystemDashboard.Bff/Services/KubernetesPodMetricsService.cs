using System.Net.Http.Headers;
using System.Text.Json;

namespace SystemDashboard.Bff.Services;

public sealed class KubernetesPodMetricsService : IKubernetesPodMetricsService
{
    private const string ServiceLabel = "app.kubernetes.io/name";
    private readonly HttpClient _httpClient;
    private readonly string _namespace;
    private readonly ILogger<KubernetesPodMetricsService> _logger;

    public KubernetesPodMetricsService(HttpClient httpClient, ILogger<KubernetesPodMetricsService> logger)
    {
        _httpClient = httpClient;
        _namespace = Environment.GetEnvironmentVariable("K8S_NAMESPACE") ?? "his-hope";
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, KubernetesServiceMetrics>> GetServiceMetricsAsync(
        IReadOnlyCollection<string> serviceNames, CancellationToken cancellationToken = default)
    {
        if (serviceNames.Count == 0)
            return new Dictionary<string, KubernetesServiceMetrics>();

        try
        {
            using var podRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/v1/namespaces/{Uri.EscapeDataString(_namespace)}/pods");
            AddServiceAccountToken(podRequest);
            using var podResponse = await _httpClient.SendAsync(podRequest, cancellationToken);
            podResponse.EnsureSuccessStatusCode();
            var pods = await podResponse.Content.ReadFromJsonAsync<PodList>(JsonOptions, cancellationToken);
            var serviceByPod = (pods?.Items ?? [])
                .Where(pod => pod.Metadata?.Name is not null)
                .ToDictionary(pod => pod.Metadata!.Name!,
                    pod => pod.Metadata?.Labels?.GetValueOrDefault(ServiceLabel),
                    StringComparer.OrdinalIgnoreCase);

            using var metricsRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"/apis/metrics.k8s.io/v1beta1/namespaces/{Uri.EscapeDataString(_namespace)}/pods");
            AddServiceAccountToken(metricsRequest);
            using var metricsResponse = await _httpClient.SendAsync(metricsRequest, cancellationToken);
            metricsResponse.EnsureSuccessStatusCode();
            var payload = await metricsResponse.Content.ReadFromJsonAsync<PodMetricsList>(JsonOptions, cancellationToken);
            var result = new Dictionary<string, KubernetesServiceMetrics>(StringComparer.OrdinalIgnoreCase);

            foreach (var pod in payload?.Items ?? [])
            {
                var service = pod.Metadata?.Name is not null && serviceByPod.TryGetValue(pod.Metadata.Name, out var mapped)
                    ? mapped
                    : null;
                if (service is null || !serviceNames.Contains(service, StringComparer.OrdinalIgnoreCase))
                    continue;

                var cpuNanocores = pod.Containers.Sum(container => ParseCpuNanocores(container.Usage?.Cpu));
                var memoryBytes = pod.Containers.Sum(container => ParseMemoryBytes(container.Usage?.Memory));
                var metrics = new KubernetesServiceMetrics(cpuNanocores / 10_000_000d, memoryBytes / 1024d / 1024d);

                if (result.TryGetValue(service, out var current))
                    result[service] = new KubernetesServiceMetrics(
                        current.CpuPercent + metrics.CpuPercent,
                        current.MemoryUsedMb + metrics.MemoryUsedMb);
                else
                    result[service] = metrics;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Kubernetes pod metrics for namespace {Namespace}", _namespace);
            return new Dictionary<string, KubernetesServiceMetrics>();
        }
    }

    private static long ParseCpuNanocores(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        if (value.EndsWith('n') && long.TryParse(value[..^1], out var n)) return n;
        if (value.EndsWith('u') && long.TryParse(value[..^1], out var u)) return u * 1_000;
        if (value.EndsWith('m') && double.TryParse(value[..^1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var millicores))
            return (long)(millicores * 1_000_000);
        return double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var cores)
            ? (long)(cores * 1_000_000_000d) : 0;
    }

    private static long ParseMemoryBytes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var suffixes = new (string Suffix, double Multiplier)[]
        {
            ("Ki", 1024), ("Mi", 1024 * 1024), ("Gi", 1024 * 1024 * 1024d),
            ("Ti", 1024d * 1024 * 1024 * 1024), ("k", 1000), ("M", 1_000_000),
            ("G", 1_000_000_000), ("T", 1_000_000_000_000d)
        };
        foreach (var (suffix, multiplier) in suffixes)
        {
            if (!value.EndsWith(suffix, StringComparison.Ordinal)) continue;
            return double.TryParse(value[..^suffix.Length], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? (long)(parsed * multiplier) : 0;
        }
        return long.TryParse(value, out var bytes) ? bytes : 0;
    }

    private static void AddServiceAccountToken(HttpRequestMessage request)
    {
        const string tokenPath = "/var/run/secrets/kubernetes.io/serviceaccount/token";
        if (File.Exists(tokenPath))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", File.ReadAllText(tokenPath).Trim());
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record PodList { public List<Pod> Items { get; init; } = []; }
    private sealed record Pod { public PodMetadata? Metadata { get; init; } }
    private sealed record PodMetricsList { public List<PodMetrics> Items { get; init; } = []; }
    private sealed record PodMetrics { public PodMetadata? Metadata { get; init; } public List<ContainerMetrics> Containers { get; init; } = []; }
    private sealed record PodMetadata { public string? Name { get; init; } public Dictionary<string, string>? Labels { get; init; } }
    private sealed record ContainerMetrics { public ResourceUsage? Usage { get; init; } }
    private sealed record ResourceUsage { public string? Cpu { get; init; } public string? Memory { get; init; } }
}
