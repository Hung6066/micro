using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Commands;

public record ApplyDiscountCommand(Guid InvoiceId, decimal Amount) : IRequest;
