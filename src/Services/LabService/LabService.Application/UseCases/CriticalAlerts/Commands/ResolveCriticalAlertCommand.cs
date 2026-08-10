using His.Hope.LabService.Application.Common.Abstractions;
using His.Hope.LabService.Application.Common.Exceptions;
using His.Hope.LabService.Application.DTOs;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Domain.ValueObjects;
using MediatR;

namespace His.Hope.LabService.Application.UseCases.CriticalAlerts.Commands;

public record ResolveCriticalAlertCommand(Guid Id) : IRequest<CriticalAlertDto>;

public class ResolveCriticalAlertCommandHandler : IRequestHandler<ResolveCriticalAlertCommand, CriticalAlertDto>
{
    private readonly ICriticalAlertRepository _alertRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ICriticalAlertRealtimePublisher _realtimePublisher;

    public ResolveCriticalAlertCommandHandler(
        ICriticalAlertRepository alertRepository,
        ICurrentUserContext currentUserContext,
        ICriticalAlertRealtimePublisher realtimePublisher)
    {
        _alertRepository = alertRepository;
        _currentUserContext = currentUserContext;
        _realtimePublisher = realtimePublisher;
    }

    public async Task<CriticalAlertDto> Handle(ResolveCriticalAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await _alertRepository.GetByIdForUpdateAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CriticalAlert), request.Id);

        EnsureCanResolve(alert);

        alert.Resolve(GetActorUserId(), GetActorDisplayName());
        _alertRepository.MarkAuditEntriesAdded(alert);
        try
        {
            await _alertRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            alert = await _alertRepository.GetByIdAsync(request.Id, cancellationToken);

            if (alert is null)
                throw;
        }

        var dto = CriticalAlertDtoMapper.ToDto(await _alertRepository.GetByIdAsync(request.Id, cancellationToken) ?? alert);
        await _realtimePublisher.PublishResolvedAsync(dto, cancellationToken);
        return dto;
    }

    private void EnsureCanResolve(CriticalAlert alert)
    {
        if (alert.Status == CriticalAlertStatus.Acknowledged &&
            !string.IsNullOrWhiteSpace(alert.AcknowledgedByUserId) &&
            alert.AcknowledgedByUserId != GetActorUserId())
        {
            throw new His.Hope.SharedKernel.Domain.Exceptions.DomainException("This critical alert is owned by another user.");
        }
    }

    private string GetActorUserId() => _currentUserContext.IsAuthenticated ? _currentUserContext.UserId : "system";

    private string GetActorDisplayName() => _currentUserContext.IsAuthenticated ? _currentUserContext.FullName : "System";
}
