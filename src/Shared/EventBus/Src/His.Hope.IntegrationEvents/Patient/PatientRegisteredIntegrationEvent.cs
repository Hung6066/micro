using His.Hope.EventBus.Abstractions;

namespace His.Hope.IntegrationEvents.Patient;

public class PatientRegisteredIntegrationEvent : IntegrationEvent
{
    public Guid PatientId { get; }
    public string FullName { get; }
    public string Phone { get; }
    public string GenderCode { get; }
    public DateTime DateOfBirth { get; }

    public PatientRegisteredIntegrationEvent(
        Guid patientId, string fullName, string phone,
        string genderCode, DateTime dateOfBirth)
    {
        PatientId = patientId;
        FullName = fullName;
        Phone = phone;
        GenderCode = genderCode;
        DateOfBirth = dateOfBirth;
    }
}
