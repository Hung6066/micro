using His.Hope.EventBus.Abstractions;

namespace His.Hope.IntegrationEvents.Lab;

public class LabOrderCreatedIntegrationEvent : IntegrationEvent
{
    public Guid LabOrderId { get; }
    public Guid PatientId { get; }
    public Guid ProviderId { get; }

    public LabOrderCreatedIntegrationEvent(
        Guid labOrderId,
        Guid patientId,
        Guid providerId)
    {
        LabOrderId = labOrderId;
        PatientId = patientId;
        ProviderId = providerId;
    }
}
