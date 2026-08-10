using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.ClinicalService.Domain.Events;

public sealed record VitalsRecordedDomainEvent(
    Guid EncounterId,
    Guid PatientId,
    decimal? Temperature,
    int? HeartRate,
    int? RespiratoryRate,
    int? SystolicBP,
    int? DiastolicBP,
    decimal? OxygenSaturation,
    DateTime OccurredOn) : IDomainEvent;
