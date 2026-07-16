using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.Repositories;
using His.Hope.ClinicalService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.ClinicalService.Infrastructure.Persistence.Repositories;

public class EncounterRepository : IEncounterRepository
{
    private readonly ClinicalDbContext _context;

    public IUnitOfWork UnitOfWork => _context;

    public EncounterRepository(ClinicalDbContext context) =>
        _context = context;

    public async Task<Encounter?> GetByIdAsync(EncounterId id, CancellationToken cancellationToken = default) =>
        await _context.Encounters
            .Include(e => e.Diagnoses)
            .Include(e => e.Procedures)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Encounter>> GetByPatientIdAsync(Guid patientId, CancellationToken cancellationToken = default) =>
        await _context.Encounters
            .Where(e => e.PatientId == patientId)
            .OrderByDescending(e => e.EncounterDate)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Encounter> Items, int TotalCount)> GetByPatientIdAsync(
        Guid patientId, int page, int pageSize, DateTime? fromDate, DateTime? toDate, CancellationToken cancellationToken = default)
    {
        var query = _context.Encounters.Where(e => e.PatientId == patientId);

        if (fromDate.HasValue)
            query = query.Where(e => e.EncounterDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(e => e.EncounterDate <= toDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.EncounterDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Encounter>> GetByProviderIdAsync(Guid providerId, CancellationToken cancellationToken = default) =>
        await _context.Encounters
            .Where(e => e.ProviderId == providerId)
            .OrderByDescending(e => e.EncounterDate)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Encounter> Items, int TotalCount)> SearchAsync(
        string searchTerm, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Encounters.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm}%";
            query = query.Where(e =>
                e.ChiefComplaint != null && EF.Functions.ILike(e.ChiefComplaint, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.EncounterDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Encounter> AddAsync(Encounter encounter, CancellationToken cancellationToken = default)
    {
        await _context.Encounters.AddAsync(encounter, cancellationToken);
        return encounter;
    }

    public Task UpdateAsync(Encounter encounter, CancellationToken cancellationToken = default)
    {
        _context.Entry(encounter).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(EncounterId id, CancellationToken cancellationToken = default) =>
        await _context.Encounters.AnyAsync(e => e.Id == id, cancellationToken);
}
