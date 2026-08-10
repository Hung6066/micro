using His.Hope.ClinicalService.Application.DTOs;
using MediatR;

namespace His.Hope.ClinicalService.Application.UseCases.Encounters.Commands;

public record StartEncounterCommand(
    Guid PatientId,
    Guid ProviderId,
    Guid? AppointmentId,
    string EncounterTypeCode,
    DateTime? EncounterDate,
    string? ChiefComplaint) : IRequest<EncounterDto>;
