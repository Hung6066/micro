using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PharmacyService.Domain.Events;

public class PrescriptionCreatedDomainEvent : DomainEvent
{
    public Guid PrescriptionId { get; }
    public Guid PatientId { get; }
    public Guid ProviderId { get; }
    public string MedicationName { get; }
    public int Quantity { get; }

    public PrescriptionCreatedDomainEvent(
        Guid prescriptionId,
        Guid patientId,
        Guid providerId,
        string medicationName,
        int quantity)
    {
        PrescriptionId = prescriptionId;
        PatientId = patientId;
        ProviderId = providerId;
        MedicationName = medicationName;
        Quantity = quantity;
    }
}
