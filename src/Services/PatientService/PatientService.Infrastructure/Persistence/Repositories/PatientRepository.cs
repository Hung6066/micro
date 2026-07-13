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

    public async Task<IReadOnlyList<Patient>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var term = searchTerm.ToLowerInvariant();
        return await _context.Patients
            .Include(p => p.Allergies)
            .Include(p => p.Conditions)
            .Where(p => p.IsActive &&
                (p.Name.FirstName.ToLower().Contains(term) ||
                 p.Name.LastName.ToLower().Contains(term) ||
                 (p.Name.MiddleName != null && p.Name.MiddleName.ToLower().Contains(term)) ||
                 p.ContactInfo.Phone.Contains(term) ||
                 (p.ContactInfo.Email != null && p.ContactInfo.Email.ToLower().Contains(term)) ||
                 (p.NationalId != null && p.NationalId.Contains(term))))
            .OrderBy(p => p.Name.LastName)
            .ThenBy(p => p.Name.FirstName)
            .ToListAsync(cancellationToken);
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
