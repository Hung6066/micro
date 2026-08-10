using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SystemDashboard.Bff.Authorization;
using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Hubs;

/// <summary>
/// SignalR hub for real-time alert push.
/// Server pushes new/updated alerts via "AlertUpdate" and "AlertCleared" events.
/// </summary>
[Authorize(Roles = DashboardRoles.ReadOnly)]
public sealed class AlertHub : Hub
{
    private readonly ILogger<AlertHub> _logger;

    public AlertHub(ILogger<AlertHub> logger) => _logger = logger;

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "alerts");
        _logger.LogInformation("Client {ConnId} subscribed to alerts", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "alerts");
        _logger.LogInformation("Client {ConnId} unsubscribed from alerts", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Called by background service or other internal components to push alert updates to all connected clients.
    /// </summary>
    public async Task PushAlertUpdate(AlertRecord alert)
    {
        await Clients.Group("alerts").SendAsync("AlertUpdate", alert);
    }

    /// <summary>
    /// Called when an alert is resolved/cleared.
    /// </summary>
    public async Task PushAlertCleared(string alertName)
    {
        await Clients.Group("alerts").SendAsync("AlertCleared", alertName);
    }
}
