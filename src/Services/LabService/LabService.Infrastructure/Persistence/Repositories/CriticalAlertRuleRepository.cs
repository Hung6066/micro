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

    public async Task<CriticalAlertRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.CriticalAlertRules.FirstOrDefaultAsync(rule => rule.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CriticalAlertRule>> ListAsync(CancellationToken cancellationToken = default) =>
        await _context.CriticalAlertRules
            .OrderByDescending(rule => rule.CreatedAt)
            .ToListAsync(cancellationToken);

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

    public void Remove(CriticalAlertRule rule) => _context.CriticalAlertRules.Remove(rule);
}
