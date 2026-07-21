using His.Hope.LabService.Application.Common.Abstractions;
using His.Hope.LabService.Application.DTOs;
using His.Hope.LabService.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace His.Hope.LabService.Api.Services;

public class CriticalAlertRealtimePublisher : ICriticalAlertRealtimePublisher
{
    private readonly IHubContext<LabCriticalAlertHub> _hubContext;

    public CriticalAlertRealtimePublisher(IHubContext<LabCriticalAlertHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishCreatedAsync(CriticalAlertDto alert, CancellationToken cancellationToken = default) =>
        PublishAsync("criticalAlertCreated", alert, cancellationToken);

    public Task PublishUpdatedAsync(CriticalAlertDto alert, CancellationToken cancellationToken = default) =>
        PublishAsync("criticalAlertUpdated", alert, cancellationToken);

    public Task PublishAcknowledgedAsync(CriticalAlertDto alert, CancellationToken cancellationToken = default) =>
        PublishAsync("criticalAlertAcknowledged", alert, cancellationToken);

    public Task PublishResolvedAsync(CriticalAlertDto alert, CancellationToken cancellationToken = default) =>
        PublishAsync("criticalAlertResolved", alert, cancellationToken);

    private Task PublishAsync(string eventName, CriticalAlertDto alert, CancellationToken cancellationToken)
    {
        return _hubContext.Clients.All.SendCoreAsync(eventName, [alert], cancellationToken);
    }
}
