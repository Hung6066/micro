using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PharmacyService.Domain.Events;

public class PrescriptionCreatedDomainEvent : DomainEvent
{
    public Guid PrescriptionId { get; }
    public Guid PatientId { get; }
    public Guid ProviderId { get; }
    public string MedicationName { get; }
    public string Strength { get; }
    public string DosageForm { get; }
    public string DosageInstructions { get; }
    public int Quantity { get; }
    public int Refills { get; }
    public DateTime PrescribedDate { get; }

    public PrescriptionCreatedDomainEvent(
        Guid prescriptionId,
        Guid patientId,
        Guid providerId,
        string medicationName,
        string strength,
        string dosageForm,
        string dosageInstructions,
        int quantity,
        int refills,
        DateTime prescribedDate)
    {
        PrescriptionId = prescriptionId;
        PatientId = patientId;
        ProviderId = providerId;
        MedicationName = medicationName;
        Strength = strength;
        DosageForm = dosageForm;
        DosageInstructions = dosageInstructions;
        Quantity = quantity;
        Refills = refills;
        PrescribedDate = prescribedDate;
    }
}
