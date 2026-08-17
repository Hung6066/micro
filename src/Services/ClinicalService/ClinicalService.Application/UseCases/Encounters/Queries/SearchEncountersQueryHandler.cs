using AutoMapper;
using His.Hope.ClinicalService.Domain.Repositories;
using His.Hope.ClinicalService.Application.DTOs;
using MediatR;

namespace His.Hope.ClinicalService.Application.UseCases.Encounters.Queries;

public class SearchEncountersQueryHandler : IRequestHandler<SearchEncountersQuery, PagedResult<EncounterDto>>
{
    private readonly IEncounterRepository _encounterRepository;
    private readonly IMapper _mapper;

    public SearchEncountersQueryHandler(IEncounterRepository encounterRepository, IMapper mapper)
    {
        _encounterRepository = encounterRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<EncounterDto>> Handle(SearchEncountersQuery request,
        CancellationToken cancellationToken)
    {
        var result = request.FacilityIds is null
            ? await _encounterRepository.SearchAsync(
                request.SearchTerm ?? string.Empty, request.Page, request.PageSize, cancellationToken)
            : await _encounterRepository.SearchAsync(
                request.SearchTerm ?? string.Empty, request.Page, request.PageSize,
                request.FacilityIds, request.CrossFacility, cancellationToken);
        var (items, totalCount) = result;

        var dtos = _mapper.Map<List<EncounterDto>>(items);

        return new PagedResult<EncounterDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}
