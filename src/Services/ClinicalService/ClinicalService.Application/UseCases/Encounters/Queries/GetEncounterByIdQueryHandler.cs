using AutoMapper;
using His.Hope.ClinicalService.Domain.Repositories;
using His.Hope.ClinicalService.Domain.ValueObjects;
using His.Hope.ClinicalService.Application.DTOs;
using MediatR;

namespace His.Hope.ClinicalService.Application.UseCases.Encounters.Queries;

public class GetEncounterByIdQueryHandler : IRequestHandler<GetEncounterByIdQuery, EncounterDto?>
{
    private readonly IEncounterRepository _encounterRepository;
    private readonly IMapper _mapper;

    public GetEncounterByIdQueryHandler(IEncounterRepository encounterRepository, IMapper mapper)
    {
        _encounterRepository = encounterRepository;
        _mapper = mapper;
    }

    public async Task<EncounterDto?> Handle(GetEncounterByIdQuery request,
        CancellationToken cancellationToken)
    {
        var encounterId = EncounterId.From(request.Id);
        var encounter = await _encounterRepository.GetByIdAsync(encounterId, cancellationToken);

        return encounter is null ? null : _mapper.Map<EncounterDto>(encounter);
    }
}
