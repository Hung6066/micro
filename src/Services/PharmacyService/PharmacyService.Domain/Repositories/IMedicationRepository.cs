using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PharmacyService.Domain.Repositories;

public interface IMedicationRepository : IRepository<Medication>
{
    Task<Medication?> GetByIdAsync(MedicationId id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Medication> Items, int TotalCount)> SearchAsync(
        string searchTerm, int page, int pageSize,
        string? category = null,
        CancellationToken cancellationToken = default);
    Task<Medication> AddAsync(Medication medication, CancellationToken cancellationToken = default);
    Task UpdateAsync(Medication medication, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(MedicationId id, CancellationToken cancellationToken = default);
}
