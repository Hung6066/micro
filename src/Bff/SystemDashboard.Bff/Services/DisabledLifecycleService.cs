using Microsoft.Extensions.Logging;

namespace SystemDashboard.Bff.Services;

/// <summary>
/// Explicitly disables host lifecycle control when no trusted orchestrator is configured.
/// This prevents the dashboard BFF from needing a Docker socket in production.
/// </summary>
public sealed class DisabledLifecycleService : IServiceLifecycleService
{
    private readonly ILogger<DisabledLifecycleService> _logger;

    public DisabledLifecycleService(ILogger<DisabledLifecycleService> logger) =>
        _logger = logger;

    public Task<bool> StartAsync(string serviceName, CancellationToken ct = default) => RejectAsync("start", serviceName);

    public Task<bool> StopAsync(string serviceName, CancellationToken ct = default) => RejectAsync("stop", serviceName);

    public Task<bool> RestartAsync(string serviceName, CancellationToken ct = default) => RejectAsync("restart", serviceName);

    private Task<bool> RejectAsync(string operation, string serviceName)
    {
        _logger.LogWarning(
            "Lifecycle operation {Operation} for {ServiceName} rejected: no trusted orchestrator is configured",
            operation, serviceName);
        return Task.FromResult(false);
    }
}
