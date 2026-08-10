using His.Hope.LabService.Application.Common.Abstractions;
using His.Hope.LabService.Application.Common.Exceptions;
using His.Hope.LabService.Application.DTOs;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.Repositories;
using MediatR;

namespace His.Hope.LabService.Application.UseCases.CriticalAlerts.Commands;

public record UpsertCriticalAlertRuleCommand(
    Guid? Id,
    string TestCode,
    string TestName,
    string? Unit,
    decimal? LowCriticalValue,
    decimal? HighCriticalValue,
    bool IsActive = true) : IRequest<CriticalAlertRuleDto>;

public class UpsertCriticalAlertRuleCommandHandler : IRequestHandler<UpsertCriticalAlertRuleCommand, CriticalAlertRuleDto>
{
    private readonly ICriticalAlertRuleRepository _ruleRepository;
    private readonly ICurrentUserContext _currentUserContext;

    public UpsertCriticalAlertRuleCommandHandler(
        ICriticalAlertRuleRepository ruleRepository,
        ICurrentUserContext currentUserContext)
    {
        _ruleRepository = ruleRepository;
        _currentUserContext = currentUserContext;
    }

    public async Task<CriticalAlertRuleDto> Handle(UpsertCriticalAlertRuleCommand request, CancellationToken cancellationToken)
    {
        CriticalAlertRule rule;

        if (request.Id is null)
        {
            rule = CriticalAlertRule.Create(
                request.TestCode,
                request.TestName,
                request.Unit,
                request.LowCriticalValue,
                request.HighCriticalValue,
                GetActorUserId(),
                GetActorDisplayName());

            await _ruleRepository.AddAsync(rule, cancellationToken);
        }
        else
        {
            rule = await _ruleRepository.GetByIdAsync(request.Id.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(CriticalAlertRule), request.Id.Value);

            rule.UpdateDetails(request.TestCode, request.TestName);
            rule.UpdateThresholds(request.LowCriticalValue, request.HighCriticalValue);
            rule.SetUnit(request.Unit);

            if (request.IsActive)
                rule.Activate();
            else
                rule.Deactivate();
        }

        await _ruleRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        return CriticalAlertDtoMapper.ToRuleDto(rule);
    }

    private string GetActorUserId() => _currentUserContext.IsAuthenticated ? _currentUserContext.UserId : "system";

    private string GetActorDisplayName() => _currentUserContext.IsAuthenticated ? _currentUserContext.FullName : "System";
}
