using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.Events;

public class LabOrderSubmittedDomainEvent : DomainEvent
{
    public Guid LabOrderId { get; }

    public LabOrderSubmittedDomainEvent(Guid labOrderId)
    {
        LabOrderId = labOrderId;
    }
}
