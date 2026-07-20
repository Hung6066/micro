using His.Hope.LabService.Domain.Entities;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.Repositories;

public interface ICriticalAlertRepository
{
    IUnitOfWork UnitOfWork { get; }
    Task<CriticalAlert?> GetCurrentAsync(Guid labOrderId, Guid labTestId, CancellationToken cancellationToken = default);
    Task<CriticalAlert?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CriticalAlert> AddAsync(CriticalAlert alert, CancellationToken cancellationToken = default);
    Task<CriticalAlert?> AddAndSaveAsync(CriticalAlert alert, Guid labOrderId, Guid labTestId, CancellationToken cancellationToken = default);
}
