using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.Events;

public class LabOrderCreatedDomainEvent : DomainEvent
{
    public Guid LabOrderId { get; }
    public Guid PatientId { get; }
    public Guid ProviderId { get; }

    public LabOrderCreatedDomainEvent(Guid labOrderId, Guid patientId, Guid providerId)
    {
        LabOrderId = labOrderId;
        PatientId = patientId;
        ProviderId = providerId;
    }
}
