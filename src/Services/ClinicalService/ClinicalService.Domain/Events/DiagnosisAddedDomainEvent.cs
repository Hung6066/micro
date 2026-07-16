using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.ClinicalService.Domain.Events;

public sealed record DiagnosisAddedDomainEvent(
    Guid EncounterId,
    string ConditionName,
    string Icd10Code,
    bool IsPrimary,
    DateTime OccurredOn) : IDomainEvent;
