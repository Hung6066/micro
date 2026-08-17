using His.Hope.BillingService.Application.DTOs;
using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Queries;

public record GetInvoicesByPatientQuery(
    Guid PatientId, IReadOnlySet<string>? FacilityIds = null, bool CrossFacility = false)
    : IRequest<IReadOnlyList<InvoiceDto>>;
