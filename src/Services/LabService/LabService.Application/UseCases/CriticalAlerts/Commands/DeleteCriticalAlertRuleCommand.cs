using His.Hope.LabService.Application.Common.Exceptions;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.Repositories;
using MediatR;

namespace His.Hope.LabService.Application.UseCases.CriticalAlerts.Commands;

public record DeleteCriticalAlertRuleCommand(Guid Id) : IRequest<Unit>;

public class DeleteCriticalAlertRuleCommandHandler : IRequestHandler<DeleteCriticalAlertRuleCommand, Unit>
{
    private readonly ICriticalAlertRuleRepository _ruleRepository;

    public DeleteCriticalAlertRuleCommandHandler(ICriticalAlertRuleRepository ruleRepository)
    {
        _ruleRepository = ruleRepository;
    }

    public async Task<Unit> Handle(DeleteCriticalAlertRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await _ruleRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CriticalAlertRule), request.Id);

        _ruleRepository.Remove(rule);
        await _ruleRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
