using His.Hope.LabService.Application.DTOs;
using His.Hope.LabService.Domain.Repositories;
using MediatR;

namespace His.Hope.LabService.Application.UseCases.CriticalAlerts.Queries;

public record GetCriticalAlertRulesQuery() : IRequest<IReadOnlyList<CriticalAlertRuleDto>>;

public class GetCriticalAlertRulesQueryHandler : IRequestHandler<GetCriticalAlertRulesQuery, IReadOnlyList<CriticalAlertRuleDto>>
{
    private readonly ICriticalAlertRuleRepository _ruleRepository;

    public GetCriticalAlertRulesQueryHandler(ICriticalAlertRuleRepository ruleRepository)
    {
        _ruleRepository = ruleRepository;
    }

    public async Task<IReadOnlyList<CriticalAlertRuleDto>> Handle(GetCriticalAlertRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await _ruleRepository.ListAsync(cancellationToken);
        return rules.Select(CriticalAlertDtoMapper.ToRuleDto).ToList();
    }
}
