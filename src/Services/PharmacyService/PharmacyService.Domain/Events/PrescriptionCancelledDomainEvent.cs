using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PharmacyService.Domain.Events;

public class PrescriptionCancelledDomainEvent : DomainEvent
{
    public Guid PrescriptionId { get; }
    public string Reason { get; }

    public PrescriptionCancelledDomainEvent(Guid prescriptionId, string reason)
    {
        PrescriptionId = prescriptionId;
        Reason = reason;
    }
}
