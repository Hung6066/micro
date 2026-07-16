using AutoMapper;
using His.Hope.ClinicalService.Application.DTOs;
using His.Hope.ClinicalService.Domain.Repositories;
using MediatR;

namespace His.Hope.ClinicalService.Application.UseCases.Encounters.Queries;

public record GetEncountersByPatientQuery(
    Guid PatientId,
    int Page = 1,
    int PageSize = 20,
    DateTime? FromDate = null,
    DateTime? ToDate = null) : IRequest<PagedResult<EncounterDto>>;

public class GetEncountersByPatientQueryHandler
    : IRequestHandler<GetEncountersByPatientQuery, PagedResult<EncounterDto>>
{
    private readonly IEncounterRepository _encounterRepository;
    private readonly IMapper _mapper;

    public GetEncountersByPatientQueryHandler(
        IEncounterRepository encounterRepository,
        IMapper mapper)
    {
        _encounterRepository = encounterRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<EncounterDto>> Handle(
        GetEncountersByPatientQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _encounterRepository.GetByPatientIdAsync(
            request.PatientId,
            request.Page,
            request.PageSize,
            request.FromDate,
            request.ToDate,
            cancellationToken);

        var dtos = _mapper.Map<List<EncounterDto>>(items);
        return new PagedResult<EncounterDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}
