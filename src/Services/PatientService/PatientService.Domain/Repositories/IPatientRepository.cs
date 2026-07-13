using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PatientService.Domain.Repositories;

public interface IPatientRepository : IRepository<Patient>
{
    Task<Patient?> GetByIdAsync(PatientId id, CancellationToken cancellationToken = default);
    Task<Patient?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Patient>> GetActivePatientsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Patient>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);
    Task<Patient> AddAsync(Patient patient, CancellationToken cancellationToken = default);
    Task UpdateAsync(Patient patient, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(PatientId id, CancellationToken cancellationToken = default);
}
