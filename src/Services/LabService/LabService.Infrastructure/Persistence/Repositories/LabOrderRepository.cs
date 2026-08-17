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
            .AsNoTracking()
            .AsSplitQuery()
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
        await GetByPatientAsync(patientId, new HashSet<string>(StringComparer.OrdinalIgnoreCase), true, cancellationToken);

    public async Task<IReadOnlyList<LabOrder>> GetByPatientAsync(
        Guid patientId, IReadOnlySet<string> facilityIds, bool crossFacility,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LabOrders
            .Include(o => o.RequestedTests)
            .ThenInclude(t => t.Result)
            .AsNoTracking()
            .AsSplitQuery()
            .Where(o => o.PatientId == patientId);
        if (!crossFacility)
        {
            if (facilityIds.Count == 0) return Array.Empty<LabOrder>();
            query = query.Where(order => order.FacilityId != null && facilityIds.Contains(order.FacilityId));
        }

        return await query
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<LabOrder> Items, int TotalCount)> SearchAsync(
        string term, int page, int pageSize,
        Guid? patientId = null, string? status = null,
        DateTime? dateFrom = null, DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
        => await SearchAsync(term, page, pageSize, patientId, status, dateFrom, dateTo,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase), true, cancellationToken);

    public async Task<(IReadOnlyList<LabOrder> Items, int TotalCount)> SearchAsync(
        string term, int page, int pageSize,
        Guid? patientId, string? status, DateTime? dateFrom, DateTime? dateTo,
        IReadOnlySet<string> facilityIds, bool crossFacility,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LabOrders
            .AsNoTracking()
            .Include(o => o.RequestedTests)
            .ThenInclude(t => t.Result)
            .AsSplitQuery()
            .AsQueryable();

        if (!crossFacility)
        {
            if (facilityIds.Count == 0) return (Array.Empty<LabOrder>(), 0);
            query = query.Where(order => order.FacilityId != null && facilityIds.Contains(order.FacilityId));
        }

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
            .ThenBy(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
