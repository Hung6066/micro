using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.PatientService.Domain.Repositories;
using His.Hope.SharedKernel.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.PatientService.Infrastructure.Persistence.Repositories;

public class PatientRepository : IPatientRepository
{
    private readonly PatientDbContext _context;

    public IUnitOfWork UnitOfWork => _context;

    public PatientRepository(PatientDbContext context) =>
        _context = context;

    public async Task<Patient?> GetByIdAsync(PatientId id, CancellationToken cancellationToken = default) =>
        await _context.Patients
            .Include(p => p.Allergies)
            .Include(p => p.Conditions)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<Patient?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default) =>
        await _context.Patients
            .Include(p => p.Allergies)
            .Include(p => p.Conditions)
            .FirstOrDefaultAsync(p => p.ContactInfo.Phone == phone && p.IsActive, cancellationToken);

    public async Task<IReadOnlyList<Patient>> GetActivePatientsAsync(CancellationToken cancellationToken = default) =>
        await _context.Patients
            .Include(p => p.Allergies)
            .Include(p => p.Conditions)
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name.LastName)
            .ThenBy(p => p.Name.FirstName)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Patient> Items, int TotalCount)> SearchAsync(
        string searchTerm, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Patients
            .Include(p => p.Allergies)
            .Include(p => p.Conditions)
            .Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Name.FirstName, pattern) ||
                EF.Functions.ILike(p.Name.LastName, pattern) ||
                (p.Name.MiddleName != null && EF.Functions.ILike(p.Name.MiddleName, pattern)) ||
                EF.Functions.ILike(p.ContactInfo.Phone, pattern) ||
                (p.ContactInfo.Email != null && EF.Functions.ILike(p.ContactInfo.Email, pattern)) ||
                (p.NationalId != null && EF.Functions.ILike(p.NationalId, pattern)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Name.LastName)
            .ThenBy(p => p.Name.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Patient> AddAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        await _context.Patients.AddAsync(patient, cancellationToken);
        return patient;
    }

    public Task UpdateAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        _context.Entry(patient).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(PatientId id, CancellationToken cancellationToken = default) =>
        await _context.Patients.AnyAsync(p => p.Id == id, cancellationToken);
}
