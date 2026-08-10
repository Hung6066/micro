using His.Hope.PharmacyService.Application.DTOs;
using MediatR;

namespace His.Hope.PharmacyService.Application.UseCases.Medications.Queries;

public record SearchMedicationsQuery(
    string SearchTerm = "",
    int Page = 1,
    int PageSize = 20,
    string? Category = null)
    : IRequest<PagedResult<MedicationDto>>;
