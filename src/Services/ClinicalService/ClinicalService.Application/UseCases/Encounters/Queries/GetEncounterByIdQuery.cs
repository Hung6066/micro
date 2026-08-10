using His.Hope.ClinicalService.Application.DTOs;
using MediatR;

namespace His.Hope.ClinicalService.Application.UseCases.Encounters.Queries;

public record GetEncounterByIdQuery(Guid Id) : IRequest<EncounterDto?>;
