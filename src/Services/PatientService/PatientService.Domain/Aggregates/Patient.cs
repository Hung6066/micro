using His.Hope.PatientService.Domain.Entities;
using His.Hope.PatientService.Domain.Events;
using His.Hope.PatientService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.ValueObjects;

namespace His.Hope.PatientService.Domain.Aggregates;

public class Patient : AggregateRoot<PatientId>
{
    public PersonName Name { get; private set; }
    public DateTime DateOfBirth { get; private set; }
    public Gender Gender { get; private set; }
    public ContactInfo ContactInfo { get; private set; }
    public Address Address { get; private set; }
    public BloodType? BloodType { get; private set; }
    public Race? Race { get; private set; }
    public MaritalStatus? MaritalStatus { get; private set; }
    public string? InsuranceId { get; private set; }
    public string? NationalId { get; private set; }
    public string? Occupation { get; private set; }
    public string? EmergencyContactName { get; private set; }
    public string? EmergencyContactPhone { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<Allergy> _allergies = [];
    public IReadOnlyCollection<Allergy> Allergies => _allergies.AsReadOnly();

    private readonly List<MedicalCondition> _conditions = [];
    public IReadOnlyCollection<MedicalCondition> Conditions => _conditions.AsReadOnly();

    private Patient(
        PatientId id,
        PersonName name,
        DateTime dateOfBirth,
        Gender gender,
        ContactInfo contactInfo,
        Address address)
        : base(id)
    {
        Name = name;
        DateOfBirth = dateOfBirth;
        Gender = gender;
        ContactInfo = contactInfo;
        Address = address;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;

        AddDomainEvent(new PatientRegisteredDomainEvent(
            Id.Value,
            name.FullName,
            dateOfBirth,
            gender.Code,
            contactInfo.Phone));
    }

    public static Patient Register(
        PersonName name,
        DateTime dateOfBirth,
        Gender gender,
        ContactInfo contactInfo,
        Address address)
    {
        Guard.Against.Null(name, nameof(name));
        Guard.Against.Null(gender, nameof(gender));
        Guard.Against.Null(contactInfo, nameof(contactInfo));
        Guard.Against.Null(address, nameof(address));

        var id = PatientId.New();

        var age = CalculateAge(dateOfBirth);
        Guard.Against.BusinessRule(new PatientMustBeAtLeastZeroYearsOld(age));

        return new Patient(id, name, dateOfBirth, gender, contactInfo, address);
    }

    public void UpdatePersonalInfo(
        PersonName name,
        DateTime? dateOfBirth,
        Gender? gender,
        ContactInfo? contactInfo,
        Address? address)
    {
        Name = Guard.Against.Null(name, nameof(name));
        ContactInfo = Guard.Against.Null(contactInfo, nameof(contactInfo));
        Address = Guard.Against.Null(address, nameof(address));

        if (dateOfBirth.HasValue)
        {
            Guard.Against.BusinessRule(new PatientMustBeAtLeastZeroYearsOld(CalculateAge(dateOfBirth.Value)));
            DateOfBirth = dateOfBirth.Value;
        }

        if (gender is not null)
            Gender = gender;

        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PatientUpdatedDomainEvent(Id.Value, name.FullName, contactInfo.Phone));
    }

    public void UpdateMedicalProfile(
        BloodType? bloodType,
        Race? race,
        MaritalStatus? maritalStatus,
        string? occupation)
    {
        BloodType = bloodType;
        Race = race;
        MaritalStatus = maritalStatus;
        Occupation = occupation;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateInsurance(string? insuranceId)
    {
        InsuranceId = insuranceId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateEmergencyContact(string? name, string? phone)
    {
        EmergencyContactName = name;
        EmergencyContactPhone = phone;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateNationalId(string? nationalId)
    {
        NationalId = nationalId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new PatientDeactivatedDomainEvent(Id.Value));
    }

    public void Reactivate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new PatientReactivatedDomainEvent(Id.Value));
    }

    public void AddAllergy(Allergy allergy)
    {
        Guard.Against.Null(allergy, nameof(allergy));
        _allergies.Add(allergy);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddCondition(MedicalCondition condition)
    {
        Guard.Against.Null(condition, nameof(condition));
        _conditions.Add(condition);
        UpdatedAt = DateTime.UtcNow;
    }

    private static int CalculateAge(DateTime dateOfBirth)
    {
        var today = DateTime.Today;
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > today.AddYears(-age)) age--;
        return age;
    }

    private Patient() { }
}

public class PatientMustBeAtLeastZeroYearsOld : IBusinessRule
{
    private readonly int _age;

    public PatientMustBeAtLeastZeroYearsOld(int age) => _age = age;

    public bool IsBroken() => _age < 0;

    public string Message => "Patient age cannot be negative.";
}
