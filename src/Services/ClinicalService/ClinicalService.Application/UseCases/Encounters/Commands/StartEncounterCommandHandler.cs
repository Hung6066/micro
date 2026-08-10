using AutoMapper;
using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.Repositories;
using His.Hope.ClinicalService.Domain.ValueObjects;
using His.Hope.ClinicalService.Application.DTOs;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.ClinicalService.Application.UseCases.Encounters.Commands;

public class StartEncounterCommandHandler : IRequestHandler<StartEncounterCommand, EncounterDto>
{
    private readonly IEncounterRepository _encounterRepository;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public StartEncounterCommandHandler(
        IEncounterRepository encounterRepository,
        IMapper mapper)
    {
        _encounterRepository = encounterRepository;
        _mapper = mapper;
        _unitOfWork = encounterRepository.UnitOfWork;
    }

    public async Task<EncounterDto> Handle(StartEncounterCommand request,
        CancellationToken cancellationToken)
    {
        var type = EncounterType.FromCode(request.EncounterTypeCode);
        var encounter = Encounter.Start(request.PatientId, request.ProviderId, type);

        if (!string.IsNullOrWhiteSpace(request.ChiefComplaint))
            encounter.ChiefComplaint = request.ChiefComplaint;

        await _encounterRepository.AddAsync(encounter, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<EncounterDto>(encounter);
    }
}
