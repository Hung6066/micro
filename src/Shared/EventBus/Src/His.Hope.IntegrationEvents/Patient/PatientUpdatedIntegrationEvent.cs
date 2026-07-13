using His.Hope.EventBus.Abstractions;

namespace His.Hope.IntegrationEvents.Patient;

public class PatientUpdatedIntegrationEvent : IntegrationEvent
{
    public Guid PatientId { get; }
    public string FullName { get; }
    public string Phone { get; }

    public PatientUpdatedIntegrationEvent(Guid patientId, string fullName, string phone)
    {
        PatientId = patientId;
        FullName = fullName;
        Phone = phone;
    }
}
