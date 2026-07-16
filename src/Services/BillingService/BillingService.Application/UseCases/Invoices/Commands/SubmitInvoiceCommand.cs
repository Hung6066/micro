using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Commands;

public record SubmitInvoiceCommand(Guid InvoiceId) : IRequest;
