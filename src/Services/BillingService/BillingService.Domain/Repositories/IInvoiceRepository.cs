using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.BillingService.Domain.Repositories;

public interface IInvoiceRepository : IRepository<Invoice>
{
    Task<Invoice?> GetByIdAsync(InvoiceId id, CancellationToken cancellationToken = default);
    Task<Invoice> AddAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task UpdateAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task RemoveAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetByPatientAsync(Guid patientId, CancellationToken cancellationToken = default);
    Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);
    Task<PagedInvoiceResult> SearchAsync(
        string term, int page, int pageSize,
        Guid? patientId = null, string? status = null,
        DateTime? dateFrom = null, DateTime? dateTo = null,
        CancellationToken cancellationToken = default);
}

public class PagedInvoiceResult
{
    public IReadOnlyList<Invoice> Items { get; }
    public int TotalCount { get; }

    public PagedInvoiceResult(IReadOnlyList<Invoice> items, int totalCount)
    {
        Items = items;
        TotalCount = totalCount;
    }
}
