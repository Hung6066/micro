using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace His.Hope.LabService.Infrastructure.Persistence.Repositories;

public class CriticalAlertRepository : ICriticalAlertRepository
{
    private readonly LabDbContext _context;

    public IUnitOfWork UnitOfWork => _context;

    public CriticalAlertRepository(LabDbContext context) => _context = context;

    public async Task<CriticalAlert?> GetCurrentAsync(Guid labOrderId, Guid labTestId, CancellationToken cancellationToken = default) =>
        await _context.CriticalAlerts
            .Include(alert => alert.AuditEntries)
            .Where(alert => alert.LabOrderId == labOrderId && alert.LabTestId == labTestId && alert.Status != CriticalAlertStatus.Resolved)
            .OrderByDescending(alert => alert.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<CriticalAlert?> GetCurrentForUpdateAsync(Guid labOrderId, Guid labTestId, CancellationToken cancellationToken = default) =>
        await _context.CriticalAlerts
            .Include(alert => alert.AuditEntries)
            .Where(alert => alert.LabOrderId == labOrderId && alert.LabTestId == labTestId && alert.Status != CriticalAlertStatus.Resolved)
            .OrderByDescending(alert => alert.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<CriticalAlert?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.CriticalAlerts
            .Include(alert => alert.AuditEntries)
            .FirstOrDefaultAsync(alert => alert.Id == id, cancellationToken);

    public async Task<CriticalAlert?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.CriticalAlerts
            .Include(alert => alert.AuditEntries)
            .FirstOrDefaultAsync(alert => alert.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CriticalAlert>> ListCurrentAsync(CancellationToken cancellationToken = default) =>
        await _context.CriticalAlerts
            .Include(alert => alert.AuditEntries)
            .Where(alert => alert.Status != CriticalAlertStatus.Resolved)
            .OrderByDescending(alert => alert.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<CriticalAlert> AddAsync(CriticalAlert alert, CancellationToken cancellationToken = default)
    {
        await _context.CriticalAlerts.AddAsync(alert, cancellationToken);
        return alert;
    }

    public void Update(CriticalAlert alert) => _context.CriticalAlerts.Update(alert);

    public void MarkAuditEntriesAdded(CriticalAlert alert)
    {
        foreach (var auditEntry in alert.AuditEntries)
        {
            var entry = _context.Entry(auditEntry);

            if (entry.State != EntityState.Unchanged)
                entry.State = EntityState.Added;
        }
    }

    public async Task<CriticalAlert?> AddAndSaveAsync(
        CriticalAlert alert,
        Guid labOrderId,
        Guid labTestId,
        CancellationToken cancellationToken = default)
    {
        await AddAsync(alert, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return alert;
        }
        catch (DbUpdateException ex) when (IsUniqueCurrentAlertViolation(ex))
        {
            Detach(alert);
            return await GetCurrentAsync(labOrderId, labTestId, cancellationToken);
        }
    }

    private void Detach(CriticalAlert alert)
    {
        _context.Entry(alert).State = EntityState.Detached;

        foreach (var auditEntry in alert.AuditEntries)
            _context.Entry(auditEntry).State = EntityState.Detached;
    }

    private static bool IsUniqueCurrentAlertViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException && postgresException.SqlState == PostgresErrorCodes.UniqueViolation;
}
