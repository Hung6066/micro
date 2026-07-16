using His.Hope.BillingService.Application.DTOs;
using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Queries;

public record GetInvoicesByPatientQuery(Guid PatientId) : IRequest<IReadOnlyList<InvoiceDto>>;
