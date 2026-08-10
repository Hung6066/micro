using His.Hope.BillingService.Application.DTOs;
using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Queries;

public record SearchInvoicesQuery(
    string Term = "",
    int Page = 1,
    int PageSize = 20,
    Guid? PatientId = null,
    string? Status = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null)
    : IRequest<PagedResult<InvoiceDto>>;
