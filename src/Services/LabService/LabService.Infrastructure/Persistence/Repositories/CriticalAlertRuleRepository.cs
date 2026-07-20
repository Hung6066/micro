using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.SharedKernel.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.LabService.Infrastructure.Persistence.Repositories;

public class CriticalAlertRuleRepository : ICriticalAlertRuleRepository
{
    private readonly LabDbContext _context;

    public IUnitOfWork UnitOfWork => _context;

    public CriticalAlertRuleRepository(LabDbContext context) => _context = context;

    public async Task<IReadOnlyList<CriticalAlertRule>> ListActiveByTestCodeAsync(string testCode, CancellationToken cancellationToken = default) =>
        await _context.CriticalAlertRules
            .Where(rule => rule.TestCode == testCode && rule.IsActive)
            .OrderByDescending(rule => rule.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<CriticalAlertRule> AddAsync(CriticalAlertRule rule, CancellationToken cancellationToken = default)
    {
        await _context.CriticalAlertRules.AddAsync(rule, cancellationToken);
        return rule;
    }
}
