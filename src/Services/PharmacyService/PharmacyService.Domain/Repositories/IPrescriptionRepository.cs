using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PharmacyService.Domain.Repositories;

public interface IPrescriptionRepository : IRepository<Prescription>
{
    Task<Prescription?> GetByIdAsync(PrescriptionId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Prescription>> GetByPatientIdAsync(Guid patientId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Prescription> Items, int TotalCount)> SearchAsync(
        string searchTerm, int page, int pageSize,
        Guid? patientId = null, string? status = null,
        CancellationToken cancellationToken = default);
    Task<Prescription> AddAsync(Prescription prescription, CancellationToken cancellationToken = default);
    Task UpdateAsync(Prescription prescription, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(PrescriptionId id, CancellationToken cancellationToken = default);
}
