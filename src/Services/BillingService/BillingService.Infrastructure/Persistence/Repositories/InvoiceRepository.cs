using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.Repositories;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.BillingService.Infrastructure.Persistence.Repositories;

public class InvoiceRepository : IInvoiceRepository
{
    private readonly BillingDbContext _context;

    public IUnitOfWork UnitOfWork => _context;

    public InvoiceRepository(BillingDbContext context) =>
        _context = context;

    public async Task<Invoice?> GetByIdAsync(InvoiceId id, CancellationToken cancellationToken = default) =>
        await _context.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<Invoice> AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        await _context.Invoices.AddAsync(invoice, cancellationToken);
        return invoice;
    }

    public Task UpdateAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        _context.Entry(invoice).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        _context.Invoices.Remove(invoice);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Invoice>> GetByPatientAsync(Guid patientId, CancellationToken cancellationToken = default) =>
        await _context.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Payments)
            .Where(i => i.PatientId == patientId)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync(cancellationToken);

    public async Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default) =>
        await _context.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber, cancellationToken);

    public async Task<PagedInvoiceResult> SearchAsync(
        string term, int page, int pageSize,
        Guid? patientId = null, string? status = null,
        DateTime? dateFrom = null, DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Payments)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{term}%";
            query = query.Where(i =>
                EF.Functions.ILike(i.InvoiceNumber, pattern) ||
                EF.Functions.ILike(i.Status.Code, pattern));
        }

        if (patientId.HasValue)
            query = query.Where(i => i.PatientId == patientId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(i => i.Status.Code == status);

        if (dateFrom.HasValue)
            query = query.Where(i => i.InvoiceDate >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(i => i.InvoiceDate <= dateTo.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(i => i.InvoiceDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedInvoiceResult(items, totalCount);
    }
}
