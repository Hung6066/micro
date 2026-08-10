namespace SystemDashboard.Bff.Services;

public interface IServiceLifecycleService
{
    Task<bool> StartAsync(string serviceName, CancellationToken ct = default);
    Task<bool> StopAsync(string serviceName, CancellationToken ct = default);
    Task<bool> RestartAsync(string serviceName, CancellationToken ct = default);
}
