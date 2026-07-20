using His.Hope.LabService.Domain.Entities;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.Repositories;

public interface ICriticalAlertRuleRepository
{
    IUnitOfWork UnitOfWork { get; }
    Task<IReadOnlyList<CriticalAlertRule>> ListActiveByTestCodeAsync(string testCode, CancellationToken cancellationToken = default);
    Task<CriticalAlertRule> AddAsync(CriticalAlertRule rule, CancellationToken cancellationToken = default);
}
