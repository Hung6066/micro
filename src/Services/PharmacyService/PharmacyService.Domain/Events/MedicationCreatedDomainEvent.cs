using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PharmacyService.Domain.Events;

public class MedicationCreatedDomainEvent : DomainEvent
{
    public Guid MedicationId { get; }
    public string Name { get; }
    public string DosageForm { get; }
    public string Strength { get; }

    public MedicationCreatedDomainEvent(
        Guid medicationId,
        string name,
        string dosageForm,
        string strength)
    {
        MedicationId = medicationId;
        Name = name;
        DosageForm = dosageForm;
        Strength = strength;
    }
}
