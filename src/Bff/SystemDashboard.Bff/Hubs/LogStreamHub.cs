using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Hubs;

[Authorize]
public class LogStreamHub : Hub
{
    private readonly ILogger<LogStreamHub> _logger;

    public LogStreamHub(ILogger<LogStreamHub> logger) => _logger = logger;

    public async Task SubscribeToService(string serviceName, string? level = null)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, serviceName);
        _logger.LogInformation("Client {ConnId} subscribed to {Service}", Context.ConnectionId, serviceName);
    }

    public async Task UnsubscribeFromService(string serviceName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, serviceName);
    }

    public async Task SendLogEntry(string serviceName, LogEntry entry)
    {
        await Clients.Group(serviceName).SendAsync("LogReceived", entry);
    }
}
