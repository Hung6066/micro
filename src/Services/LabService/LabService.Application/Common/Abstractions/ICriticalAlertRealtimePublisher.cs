using His.Hope.LabService.Application.DTOs;

namespace His.Hope.LabService.Application.Common.Abstractions;

public interface ICriticalAlertRealtimePublisher
{
    Task PublishCreatedAsync(CriticalAlertDto alert, CancellationToken cancellationToken = default);
    Task PublishUpdatedAsync(CriticalAlertDto alert, CancellationToken cancellationToken = default);
    Task PublishAcknowledgedAsync(CriticalAlertDto alert, CancellationToken cancellationToken = default);
    Task PublishResolvedAsync(CriticalAlertDto alert, CancellationToken cancellationToken = default);
}
