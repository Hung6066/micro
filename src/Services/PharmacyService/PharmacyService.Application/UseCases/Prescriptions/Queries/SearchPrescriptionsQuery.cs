using His.Hope.PharmacyService.Application.DTOs;
using MediatR;

namespace His.Hope.PharmacyService.Application.UseCases.Prescriptions.Queries;

public record SearchPrescriptionsQuery(
    string SearchTerm = "",
    int Page = 1,
    int PageSize = 20,
    Guid? PatientId = null,
    string? Status = null,
    IReadOnlySet<string>? FacilityIds = null,
    bool CrossFacility = false)
    : IRequest<PagedResult<PrescriptionDto>>;
