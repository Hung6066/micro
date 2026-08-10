using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PharmacyService.Domain.Events;

public class PrescriptionFilledDomainEvent : DomainEvent
{
    public Guid PrescriptionId { get; }
    public DateTime FilledDate { get; }

    public PrescriptionFilledDomainEvent(Guid prescriptionId, DateTime filledDate)
    {
        PrescriptionId = prescriptionId;
        FilledDate = filledDate;
    }
}
