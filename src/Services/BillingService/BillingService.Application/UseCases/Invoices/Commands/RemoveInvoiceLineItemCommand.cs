using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Commands;

public record RemoveInvoiceLineItemCommand(Guid InvoiceId, Guid LineItemId) : IRequest;
