using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PharmacyService.Domain.Events;

public class MedicationReactivatedDomainEvent : DomainEvent
{
    public Guid MedicationId { get; }

    public MedicationReactivatedDomainEvent(Guid medicationId) =>
        MedicationId = medicationId;
}
