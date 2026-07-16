using His.Hope.BillingService.Application.DTOs;
using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Queries;

public record GetInvoiceByIdQuery(Guid Id) : IRequest<InvoiceDto?>;
