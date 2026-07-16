using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PharmacyService.Domain.Events;

public class MedicationDeactivatedDomainEvent : DomainEvent
{
    public Guid MedicationId { get; }

    public MedicationDeactivatedDomainEvent(Guid medicationId) =>
        MedicationId = medicationId;
}
