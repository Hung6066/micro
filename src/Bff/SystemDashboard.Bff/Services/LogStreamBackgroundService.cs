using SystemDashboard.Bff.Hubs;
using SystemDashboard.Bff.Models;
using Microsoft.AspNetCore.SignalR;

namespace SystemDashboard.Bff.Services;

public sealed class LogStreamBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LogStreamBackgroundService> _logger;
    private DateTime _lastPushedTimestamp = DateTime.UtcNow - TimeSpan.FromSeconds(30);

    public LogStreamBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<LogStreamBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LogStreamBackgroundService started (cursor: {Cursor})", _lastPushedTimestamp.ToString("o"));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var esQuery = scope.ServiceProvider.GetRequiredService<IElasticsearchQueryService>();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<LogStreamHub>>();

                var logs = await esQuery.QueryLogsAsync(
                    size: 50,
                    afterTimestamp: _lastPushedTimestamp,
                    ct: stoppingToken);

                if (logs.Count > 0)
                {
                    _logger.LogDebug("Pushing {Count} new log entries via SignalR", logs.Count);
                    foreach (var log in logs)
                    {
                        await hubContext.Clients
                            .Group(log.Service ?? "*")
                            .SendAsync("LogEntry", log, stoppingToken);
                    }

                    var maxTs = logs.Max(l => l.Timestamp);
                    if (maxTs > _lastPushedTimestamp)
                        _lastPushedTimestamp = maxTs;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LogStreamBackgroundService poll failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
