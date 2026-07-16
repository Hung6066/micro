using MediatR;

namespace His.Hope.ClinicalService.Application.UseCases.Encounters.Commands;

public record CompleteEncounterCommand(Guid EncounterId) : IRequest<Unit>;
