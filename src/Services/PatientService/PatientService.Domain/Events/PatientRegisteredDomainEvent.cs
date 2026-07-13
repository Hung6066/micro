using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PatientService.Domain.Events;

public class PatientRegisteredDomainEvent : DomainEvent
{
    public Guid PatientId { get; }
    public string FullName { get; }
    public DateTime DateOfBirth { get; }
    public string GenderCode { get; }
    public string Phone { get; }

    public PatientRegisteredDomainEvent(
        Guid patientId,
        string fullName,
        DateTime dateOfBirth,
        string genderCode,
        string phone)
    {
        PatientId = patientId;
        FullName = fullName;
        DateOfBirth = dateOfBirth;
        GenderCode = genderCode;
        Phone = phone;
    }
}
