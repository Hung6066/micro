using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SystemDashboard.Bff.Hubs;

/// <summary>
/// SignalR hub for real-time metric streaming.
/// Clients subscribe to service groups; the server pushes MetricUpdate messages.
/// </summary>
[Authorize]
public class MetricsHub : Hub
{
    private readonly ILogger<MetricsHub> _logger;

    public MetricsHub(ILogger<MetricsHub> logger) => _logger = logger;

    public async Task SubscribeMetrics(string serviceName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, serviceName);
        _logger.LogInformation("Client {ConnId} subscribed to metrics for {Service}", Context.ConnectionId, serviceName);
    }

    public async Task UnsubscribeMetrics(string serviceName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, serviceName);
        _logger.LogInformation("Client {ConnId} unsubscribed from metrics for {Service}", Context.ConnectionId, serviceName);
    }
}
