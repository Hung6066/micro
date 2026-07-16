using His.Hope.BillingService.Application.DTOs;
using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Commands;

public record AddInvoiceLineItemCommand(
    Guid InvoiceId,
    string Description,
    int Quantity,
    decimal UnitPrice,
    string? ItemCode,
    string? ItemTypeCode) : IRequest<InvoiceDto>;
