using His.Hope.ClinicalService.Application.DTOs;
using MediatR;

namespace His.Hope.ClinicalService.Application.UseCases.Encounters.Commands;

public record RecordVitalsCommand(
    Guid EncounterId,
    decimal? Temperature,
    int? HeartRate,
    int? RespiratoryRate,
    int? SystolicBP,
    int? DiastolicBP,
    decimal? OxygenSaturation,
    decimal? HeightCm,
    decimal? WeightKg,
    decimal? Bmi) : IRequest<EncounterDto>;
