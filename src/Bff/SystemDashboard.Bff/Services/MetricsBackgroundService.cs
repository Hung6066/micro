using SystemDashboard.Bff.Aggregators;
using SystemDashboard.Bff.Hubs;
using SystemDashboard.Bff.Models;
using Microsoft.AspNetCore.SignalR;

namespace SystemDashboard.Bff.Services;

/// <summary>
/// Background service that polls Prometheus every 2 seconds for CPU, memory, and request rate
/// across all 7 microservices and pushes the results via SignalR to subscribed groups.
/// </summary>
public sealed class MetricsBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MetricsBackgroundService> _logger;

    private static readonly string[] ServiceNames =
    [
        "identity-service",
        "patient-service",
        "appointment-service",
        "clinical-service",
        "lab-service",
        "billing-service",
        "pharmacy-service",
    ];

    public MetricsBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<MetricsBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MetricsBackgroundService started — polling every 2s for {Count} services", ServiceNames.Length);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var aggregator = scope.ServiceProvider.GetRequiredService<IMetricsAggregator>();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<MetricsHub>>();

                var tasks = ServiceNames.Select(async serviceName =>
                {
                    try
                    {
                        var snapshots = await aggregator.GetMetricsAsync(
                            serviceName, ["cpu", "memory", "requests"], "5m", stoppingToken);

                        var update = new LiveMetricUpdate
                        {
                            ServiceName = serviceName,
                            Cpu = snapshots.FirstOrDefault(s => s.Name == "cpu")?.CurrentValue ?? 0,
                            Memory = snapshots.FirstOrDefault(s => s.Name == "memory")?.CurrentValue ?? 0,
                            Requests = snapshots.FirstOrDefault(s => s.Name == "requests")?.CurrentValue ?? 0,
                            Timestamp = DateTime.UtcNow,
                        };

                        await hubContext.Clients
                            .Group(serviceName)
                            .SendAsync("MetricUpdate", update, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch/push metrics for {Service}", serviceName);
                    }
                });

                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MetricsBackgroundService poll cycle failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
