using AutoMapper;
using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.Repositories;
using His.Hope.ClinicalService.Domain.ValueObjects;
using His.Hope.ClinicalService.Application.Common.Exceptions;
using His.Hope.ClinicalService.Application.DTOs;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.ClinicalService.Application.UseCases.Encounters.Commands;

public class AddDiagnosisCommandHandler : IRequestHandler<AddDiagnosisCommand, EncounterDto>
{
    private readonly IEncounterRepository _encounterRepository;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public AddDiagnosisCommandHandler(
        IEncounterRepository encounterRepository,
        IMapper mapper)
    {
        _encounterRepository = encounterRepository;
        _mapper = mapper;
        _unitOfWork = encounterRepository.UnitOfWork;
    }

    public async Task<EncounterDto> Handle(AddDiagnosisCommand request,
        CancellationToken cancellationToken)
    {
        var encounterId = EncounterId.From(request.EncounterId);
        var encounter = await _encounterRepository.GetByIdAsync(encounterId, cancellationToken);

        if (encounter is null)
            throw new NotFoundException(nameof(Encounter), request.EncounterId);

        var diagnosis = new Diagnosis(
            request.ConditionName,
            request.Icd10Code,
            request.IsPrimary,
            request.Notes);

        encounter.AddDiagnosis(diagnosis);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<EncounterDto>(encounter);
    }
}
