using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.Events;

public class LabOrderCancelledDomainEvent : DomainEvent
{
    public Guid LabOrderId { get; }
    public string Reason { get; }

    public LabOrderCancelledDomainEvent(Guid labOrderId, string reason)
    {
        LabOrderId = labOrderId;
        Reason = reason;
    }
}
