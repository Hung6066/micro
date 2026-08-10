using AutoMapper;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Application.DTOs;
using MediatR;

namespace His.Hope.PharmacyService.Application.UseCases.Prescriptions.Queries;

public class SearchPrescriptionsQueryHandler : IRequestHandler<SearchPrescriptionsQuery, PagedResult<PrescriptionDto>>
{
    private readonly IPrescriptionRepository _prescriptionRepository;
    private readonly IMapper _mapper;

    public SearchPrescriptionsQueryHandler(IPrescriptionRepository prescriptionRepository, IMapper mapper)
    {
        _prescriptionRepository = prescriptionRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<PrescriptionDto>> Handle(SearchPrescriptionsQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _prescriptionRepository.SearchAsync(
            request.SearchTerm, request.Page, request.PageSize,
            request.PatientId, request.Status, cancellationToken);

        var dtos = _mapper.Map<List<PrescriptionDto>>(items);

        return new PagedResult<PrescriptionDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}
