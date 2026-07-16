using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.PharmacyService.Infrastructure.Persistence.Repositories;

public class PrescriptionRepository : IPrescriptionRepository
{
    private readonly PharmacyDbContext _context;

    public IUnitOfWork UnitOfWork => _context;

    public PrescriptionRepository(PharmacyDbContext context) =>
        _context = context;

    public async Task<Prescription?> GetByIdAsync(PrescriptionId id, CancellationToken cancellationToken = default) =>
        await _context.Prescriptions
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Prescription>> GetByPatientIdAsync(Guid patientId, CancellationToken cancellationToken = default) =>
        await _context.Prescriptions
            .Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.PrescribedDate)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Prescription> Items, int TotalCount)> SearchAsync(
        string searchTerm, int page, int pageSize,
        Guid? patientId = null, string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Prescriptions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.MedicationName, pattern));
        }

        if (patientId.HasValue)
            query = query.Where(p => p.PatientId == patientId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status.Code == status);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.PrescribedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Prescription> AddAsync(Prescription prescription, CancellationToken cancellationToken = default)
    {
        await _context.Prescriptions.AddAsync(prescription, cancellationToken);
        return prescription;
    }

    public Task UpdateAsync(Prescription prescription, CancellationToken cancellationToken = default)
    {
        _context.Entry(prescription).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(PrescriptionId id, CancellationToken cancellationToken = default) =>
        await _context.Prescriptions.AnyAsync(p => p.Id == id, cancellationToken);
}
