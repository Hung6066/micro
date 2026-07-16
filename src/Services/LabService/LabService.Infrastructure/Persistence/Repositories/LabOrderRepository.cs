using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.SharedKernel.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.LabService.Infrastructure.Persistence.Repositories;

public class LabOrderRepository : ILabOrderRepository
{
    private readonly LabDbContext _context;

    public IUnitOfWork UnitOfWork => _context;

    public LabOrderRepository(LabDbContext context) =>
        _context = context;

    public async Task<LabOrder?> GetByIdAsync(LabOrderId id, CancellationToken cancellationToken = default) =>
        await _context.LabOrders
            .Include(o => o.RequestedTests)
            .ThenInclude(t => t.Result)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<LabOrder> AddAsync(LabOrder labOrder, CancellationToken cancellationToken = default)
    {
        await _context.LabOrders.AddAsync(labOrder, cancellationToken);
        return labOrder;
    }

    public void Update(LabOrder labOrder) =>
        _context.Entry(labOrder).State = EntityState.Modified;

    public void Remove(LabOrder labOrder) =>
        _context.LabOrders.Remove(labOrder);

    public async Task<IReadOnlyList<LabOrder>> GetByPatientAsync(Guid patientId, CancellationToken cancellationToken = default) =>
        await _context.LabOrders
            .Include(o => o.RequestedTests)
            .ThenInclude(t => t.Result)
            .Where(o => o.PatientId == patientId)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<LabOrder> Items, int TotalCount)> SearchAsync(
        string term, int page, int pageSize,
        Guid? patientId = null, string? status = null,
        DateTime? dateFrom = null, DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LabOrders
            .Include(o => o.RequestedTests)
            .ThenInclude(t => t.Result)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{term}%";
            query = query.Where(o =>
                o.Notes != null && EF.Functions.ILike(o.Notes, pattern));
        }

        if (patientId.HasValue)
            query = query.Where(o => o.PatientId == patientId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(o => o.Status.Code == status);

        if (dateFrom.HasValue)
            query = query.Where(o => o.OrderDate >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(o => o.OrderDate <= dateTo.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
