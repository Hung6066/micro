using His.Hope.BillingService.Application.DTOs;
using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Commands;

public record RecordPaymentCommand(
    Guid InvoiceId,
    Guid PatientId,
    decimal Amount,
    DateTime PaymentDate,
    string MethodCode,
    string? ReferenceNumber,
    string? Notes) : IRequest<InvoiceDto>;
