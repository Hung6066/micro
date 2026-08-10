using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Entities;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.Repositories;

public interface ILabOrderRepository : IRepository<LabOrder>
{
    Task<LabOrder?> GetByIdAsync(LabOrderId id, CancellationToken cancellationToken = default);
    Task<LabOrder> AddAsync(LabOrder labOrder, CancellationToken cancellationToken = default);
    void Update(LabOrder labOrder);
    void Remove(LabOrder labOrder);
    Task<IReadOnlyList<LabOrder>> GetByPatientAsync(Guid patientId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<LabOrder> Items, int TotalCount)> SearchAsync(
        string term, int page, int pageSize,
        Guid? patientId = null, string? status = null,
        DateTime? dateFrom = null, DateTime? dateTo = null,
        CancellationToken cancellationToken = default);
}
