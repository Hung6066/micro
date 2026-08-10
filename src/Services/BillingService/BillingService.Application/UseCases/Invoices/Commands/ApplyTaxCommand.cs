using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Commands;

public record ApplyTaxCommand(Guid InvoiceId, decimal Amount) : IRequest;
