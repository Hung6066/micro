using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Commands;

public record CancelInvoiceCommand(Guid InvoiceId, string Reason) : IRequest;
