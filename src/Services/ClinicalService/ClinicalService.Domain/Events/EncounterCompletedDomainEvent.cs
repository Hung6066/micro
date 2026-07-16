using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.ClinicalService.Domain.Events;

public sealed record EncounterCompletedDomainEvent(
    Guid EncounterId,
    Guid PatientId,
    DateTime CompletedAt,
    DateTime OccurredOn) : IDomainEvent;
