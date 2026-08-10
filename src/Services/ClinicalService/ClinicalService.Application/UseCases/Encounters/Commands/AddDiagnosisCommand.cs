using His.Hope.ClinicalService.Application.DTOs;
using MediatR;

namespace His.Hope.ClinicalService.Application.UseCases.Encounters.Commands;

public record AddDiagnosisCommand(
    Guid EncounterId,
    string ConditionName,
    string Icd10Code,
    bool IsPrimary,
    string? Notes) : IRequest<EncounterDto>;
