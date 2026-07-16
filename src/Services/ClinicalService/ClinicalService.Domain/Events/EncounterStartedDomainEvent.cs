using His.Hope.ClinicalService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.ClinicalService.Domain.Events;

public sealed record EncounterStartedDomainEvent(
    Guid EncounterId,
    Guid PatientId,
    Guid ProviderId,
    Guid? AppointmentId,
    EncounterType EncounterType,
    DateTime OccurredOn) : IDomainEvent;
