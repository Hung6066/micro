using His.Hope.LabService.Application.DTOs;
using His.Hope.LabService.Domain.Repositories;
using MediatR;

namespace His.Hope.LabService.Application.UseCases.CriticalAlerts.Queries;

public record GetCriticalAlertsQuery() : IRequest<IReadOnlyList<CriticalAlertDto>>;

public class GetCriticalAlertsQueryHandler : IRequestHandler<GetCriticalAlertsQuery, IReadOnlyList<CriticalAlertDto>>
{
    private readonly ICriticalAlertRepository _alertRepository;

    public GetCriticalAlertsQueryHandler(ICriticalAlertRepository alertRepository)
    {
        _alertRepository = alertRepository;
    }

    public async Task<IReadOnlyList<CriticalAlertDto>> Handle(GetCriticalAlertsQuery request, CancellationToken cancellationToken)
    {
        var alerts = await _alertRepository.ListCurrentAsync(cancellationToken);
        return alerts.Select(CriticalAlertDtoMapper.ToDto).ToList();
    }
}
