namespace SystemDashboard.Bff.Services;

public sealed record KubernetesServiceMetrics(double CpuPercent, double MemoryUsedMb);

public interface IKubernetesPodMetricsService
{
    Task<IReadOnlyDictionary<string, KubernetesServiceMetrics>> GetServiceMetricsAsync(
        IReadOnlyCollection<string> serviceNames,
        CancellationToken cancellationToken = default);
}
