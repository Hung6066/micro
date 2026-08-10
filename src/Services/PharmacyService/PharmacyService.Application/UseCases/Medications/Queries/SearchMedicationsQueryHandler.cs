using AutoMapper;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Application.DTOs;
using MediatR;

namespace His.Hope.PharmacyService.Application.UseCases.Medications.Queries;

public class SearchMedicationsQueryHandler : IRequestHandler<SearchMedicationsQuery, PagedResult<MedicationDto>>
{
    private readonly IMedicationRepository _medicationRepository;
    private readonly IMapper _mapper;

    public SearchMedicationsQueryHandler(IMedicationRepository medicationRepository, IMapper mapper)
    {
        _medicationRepository = medicationRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<MedicationDto>> Handle(SearchMedicationsQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _medicationRepository.SearchAsync(
            request.SearchTerm, request.Page, request.PageSize, request.Category, cancellationToken);

        var dtos = _mapper.Map<List<MedicationDto>>(items);

        return new PagedResult<MedicationDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}
