using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PatientService.Domain.Events;

public class PatientUpdatedDomainEvent : DomainEvent
{
    public Guid PatientId { get; }
    public string FullName { get; }
    public string Phone { get; }

    public PatientUpdatedDomainEvent(Guid patientId, string fullName, string phone)
    {
        PatientId = patientId;
        FullName = fullName;
        Phone = phone;
    }
}
