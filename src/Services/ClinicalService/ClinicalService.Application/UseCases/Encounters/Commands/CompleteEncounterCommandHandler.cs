using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.Repositories;
using His.Hope.ClinicalService.Domain.ValueObjects;
using His.Hope.ClinicalService.Application.Common.Exceptions;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.ClinicalService.Application.UseCases.Encounters.Commands;

public class CompleteEncounterCommandHandler : IRequestHandler<CompleteEncounterCommand, Unit>
{
    private readonly IEncounterRepository _encounterRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteEncounterCommandHandler(IEncounterRepository encounterRepository)
    {
        _encounterRepository = encounterRepository;
        _unitOfWork = encounterRepository.UnitOfWork;
    }

    public async Task<Unit> Handle(CompleteEncounterCommand request,
        CancellationToken cancellationToken)
    {
        var encounterId = EncounterId.From(request.EncounterId);
        var encounter = await _encounterRepository.GetByIdAsync(encounterId, cancellationToken);

        if (encounter is null)
            throw new NotFoundException(nameof(Encounter), request.EncounterId);

        encounter.Complete();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
