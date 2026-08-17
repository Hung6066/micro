using His.Hope.ClinicalService.Application.DTOs;
using MediatR;

namespace His.Hope.ClinicalService.Application.UseCases.Encounters.Queries;

public record SearchEncountersQuery(
    string? SearchTerm,
    int Page = 1,
    int PageSize = 20,
    IReadOnlySet<string>? FacilityIds = null,
    bool CrossFacility = false)
    : IRequest<PagedResult<EncounterDto>>;
