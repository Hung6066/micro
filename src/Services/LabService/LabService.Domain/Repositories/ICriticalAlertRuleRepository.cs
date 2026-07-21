using His.Hope.LabService.Domain.Entities;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.Repositories;

public interface ICriticalAlertRuleRepository
{
    IUnitOfWork UnitOfWork { get; }
    Task<CriticalAlertRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CriticalAlertRule>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CriticalAlertRule>> ListActiveByTestCodeAsync(string testCode, CancellationToken cancellationToken = default);
    Task<CriticalAlertRule> AddAsync(CriticalAlertRule rule, CancellationToken cancellationToken = default);
    void Remove(CriticalAlertRule rule);
}
