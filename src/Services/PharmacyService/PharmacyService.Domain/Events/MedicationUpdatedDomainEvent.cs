using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PharmacyService.Domain.Events;

public class MedicationUpdatedDomainEvent : DomainEvent
{
    public Guid MedicationId { get; }
    public string Name { get; }

    public MedicationUpdatedDomainEvent(Guid medicationId, string name)
    {
        MedicationId = medicationId;
        Name = name;
    }
}
