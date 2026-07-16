using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.PharmacyService.Infrastructure.Persistence.Repositories;

public class MedicationRepository : IMedicationRepository
{
    private readonly PharmacyDbContext _context;

    public IUnitOfWork UnitOfWork => _context;

    public MedicationRepository(PharmacyDbContext context) =>
        _context = context;

    public async Task<Medication?> GetByIdAsync(MedicationId id, CancellationToken cancellationToken = default) =>
        await _context.Medications
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<Medication> Items, int TotalCount)> SearchAsync(
        string searchTerm, int page, int pageSize,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Medications
            .Where(m => m.IsActive);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm}%";
            query = query.Where(m =>
                EF.Functions.ILike(m.Name, pattern) ||
                (m.GenericName != null && EF.Functions.ILike(m.GenericName, pattern)) ||
                (m.BrandName != null && EF.Functions.ILike(m.BrandName, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(m => m.Category == category);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(m => m.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Medication> AddAsync(Medication medication, CancellationToken cancellationToken = default)
    {
        await _context.Medications.AddAsync(medication, cancellationToken);
        return medication;
    }

    public Task UpdateAsync(Medication medication, CancellationToken cancellationToken = default)
    {
        _context.Entry(medication).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(MedicationId id, CancellationToken cancellationToken = default) =>
        await _context.Medications.AnyAsync(m => m.Id == id, cancellationToken);
}
