using His.Hope.EventBus.Abstractions;

namespace His.Hope.IntegrationEvents.Lab;

public class LabOrderSubmittedIntegrationEvent : IntegrationEvent
{
    public Guid LabOrderId { get; }

    public LabOrderSubmittedIntegrationEvent(Guid labOrderId)
    {
        LabOrderId = labOrderId;
    }
}
