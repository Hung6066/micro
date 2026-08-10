using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PatientService.Domain.Events;

public class PatientReactivatedDomainEvent : DomainEvent
{
    public Guid PatientId { get; }

    public PatientReactivatedDomainEvent(Guid patientId) =>
        PatientId = patientId;
}
