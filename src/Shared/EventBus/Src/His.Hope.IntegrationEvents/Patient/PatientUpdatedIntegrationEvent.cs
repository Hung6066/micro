using His.Hope.EventBus.Abstractions;

namespace His.Hope.IntegrationEvents.Patient;

public class PatientUpdatedIntegrationEvent : IntegrationEvent
{
    public Guid PatientId { get; }
    public string FullName { get; }
    public string Phone { get; }
    public string? FacilityId { get; }

    public PatientUpdatedIntegrationEvent(Guid patientId, string fullName, string phone)
    {
        PatientId = patientId;
        FullName = fullName;
        Phone = phone;
    }

    public PatientUpdatedIntegrationEvent(Guid patientId, string fullName, string phone, string? facilityId)
        : this(patientId, fullName, phone)
    {
        FacilityId = facilityId;
    }
}
