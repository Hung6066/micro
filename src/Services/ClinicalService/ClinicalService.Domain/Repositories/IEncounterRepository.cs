using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.ClinicalService.Domain.Repositories;

public interface IEncounterRepository : IRepository<Encounter>
{
    Task<Encounter?> GetByIdAsync(EncounterId id, CancellationToken ct = default);
    Task<IReadOnlyList<Encounter>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<(IReadOnlyList<Encounter> Items, int TotalCount)> GetByPatientIdAsync(
        Guid patientId, int page, int pageSize, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);
    Task<IReadOnlyList<Encounter>> GetByProviderIdAsync(Guid providerId, CancellationToken ct = default);
    Task<(IReadOnlyList<Encounter> Items, int TotalCount)> SearchAsync(string searchTerm, int page, int pageSize, CancellationToken ct = default);
    Task<Encounter> AddAsync(Encounter encounter, CancellationToken ct = default);
    Task UpdateAsync(Encounter encounter, CancellationToken ct = default);
    Task<bool> ExistsAsync(EncounterId id, CancellationToken ct = default);
}
