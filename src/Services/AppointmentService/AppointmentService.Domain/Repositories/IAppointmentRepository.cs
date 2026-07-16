using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.AppointmentService.Domain.Repositories;

public interface IAppointmentRepository : IRepository<Appointment>
{
    Task<Appointment?> GetByIdAsync(AppointmentId id, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<(IReadOnlyList<Appointment> Items, int TotalCount)> GetByPatientIdAsync(
        Guid patientId, int page, int pageSize, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> GetByProviderIdAsync(Guid providerId, CancellationToken ct = default);
    Task<(IReadOnlyList<Appointment> Items, int TotalCount)> SearchAsync(string searchTerm, int page, int pageSize, CancellationToken ct = default);
    Task<Appointment> AddAsync(Appointment appointment, CancellationToken ct = default);
    Task UpdateAsync(Appointment appointment, CancellationToken ct = default);
    Task<bool> ExistsAsync(AppointmentId id, CancellationToken ct = default);
}
