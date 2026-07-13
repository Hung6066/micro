using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PatientService.Domain.Events;

public class PatientDeactivatedDomainEvent : DomainEvent
{
    public Guid PatientId { get; }

    public PatientDeactivatedDomainEvent(Guid patientId) =>
        PatientId = patientId;
}
