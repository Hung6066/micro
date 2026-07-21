using SystemDashboard.Bff.Services;

namespace SystemDashboard.Bff.Aggregators;

public sealed class LifecycleController : ILifecycleController
{
    private readonly IServiceLifecycleService _lifecycleService;
    private readonly ILogger<LifecycleController> _logger;

    public LifecycleController(
        IServiceLifecycleService lifecycleService,
        ILogger<LifecycleController> logger)
    {
        _lifecycleService = lifecycleService;
        _logger = logger;
    }

    public async Task<bool> StartAsync(string serviceName, CancellationToken ct = default)
    {
        _logger.LogInformation("LifecycleController: starting service {ServiceName}", serviceName);
        return await _lifecycleService.StartAsync(serviceName, ct);
    }

    public async Task<bool> StopAsync(string serviceName, CancellationToken ct = default)
    {
        _logger.LogInformation("LifecycleController: stopping service {ServiceName}", serviceName);
        return await _lifecycleService.StopAsync(serviceName, ct);
    }

    public async Task<bool> RestartAsync(string serviceName, CancellationToken ct = default)
    {
        _logger.LogInformation("LifecycleController: restarting service {ServiceName}", serviceName);
        return await _lifecycleService.RestartAsync(serviceName, ct);
    }
}
