using FluentAssertions;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.PatientService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Exceptions;
using His.Hope.SharedKernel.Domain.ValueObjects;

namespace His.Hope.PatientService.Domain.Tests;

public class PatientTests
{
    private static readonly PersonName DefaultName = new("John", "Doe", "M");
    private static readonly DateTime DefaultDob = new(1990, 1, 15);
    private static readonly Gender DefaultGender = Gender.Male;
    private static readonly ContactInfo DefaultContact = new("+1234567890", "john@example.com");
    private static readonly Address DefaultAddress = new("123 Main St", "Downtown", "Metropolis", "State", "12345", "USA");

    private Patient CreateDefaultPatient()
    {
        return Patient.Register(DefaultName, DefaultDob, DefaultGender, DefaultContact, DefaultAddress);
    }

    [Fact]
    public void Register_WithValidParameters_ShouldCreateActivePatient()
    {
        // Act
        var patient = Patient.Register(DefaultName, DefaultDob, DefaultGender, DefaultContact, DefaultAddress);

        // Assert
        patient.Should().NotBeNull();
        patient.Name.Should().Be(DefaultName);
        patient.DateOfBirth.Should().Be(DefaultDob);
        patient.Gender.Should().Be(DefaultGender);
        patient.ContactInfo.Should().Be(DefaultContact);
        patient.Address.Should().Be(DefaultAddress);
        patient.IsActive.Should().BeTrue();
        patient.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        patient.Allergies.Should().BeEmpty();
        patient.Conditions.Should().BeEmpty();
        patient.BloodType.Should().BeNull();
        patient.Race.Should().BeNull();
        patient.MaritalStatus.Should().BeNull();
        patient.InsuranceId.Should().BeNull();
        patient.NationalId.Should().BeNull();
        patient.Occupation.Should().BeNull();
        patient.EmergencyContactName.Should().BeNull();
        patient.EmergencyContactPhone.Should().BeNull();
    }

    [Fact]
    public void Register_WithNullName_ShouldThrow()
    {
        // Act
        var act = () => Patient.Register(null!, DefaultDob, DefaultGender, DefaultContact, DefaultAddress);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("name");
    }

    [Fact]
    public void Register_WithNullGender_ShouldThrow()
    {
        // Act
        var act = () => Patient.Register(DefaultName, DefaultDob, null!, DefaultContact, DefaultAddress);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("gender");
    }

    [Fact]
    public void Register_WithNullContactInfo_ShouldThrow()
    {
        // Act
        var act = () => Patient.Register(DefaultName, DefaultDob, DefaultGender, null!, DefaultAddress);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("contactInfo");
    }

    [Fact]
    public void Register_WithNullAddress_ShouldThrow()
    {
        // Act
        var act = () => Patient.Register(DefaultName, DefaultDob, DefaultGender, DefaultContact, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("address");
    }

    [Fact]
    public void Register_WithFutureDateOfBirth_ShouldThrowDomainException()
    {
        // Arrange
        var futureDob = DateTime.Today.AddDays(1);

        // Act
        var act = () => Patient.Register(DefaultName, futureDob, DefaultGender, DefaultContact, DefaultAddress);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("Patient age cannot be negative.");
    }

    [Fact]
    public void Register_WithTodayAsDateOfBirth_ShouldCreatePatient()
    {
        // Arrange
        var todayDob = DateTime.Today;

        // Act
        var patient = Patient.Register(DefaultName, todayDob, DefaultGender, DefaultContact, DefaultAddress);

        // Assert
        patient.Should().NotBeNull();
        patient.DateOfBirth.Should().Be(todayDob);
    }

    [Fact]
    public void UpdatePersonalInfo_WithNewNameAndContact_ShouldUpdate()
    {
        // Arrange
        var patient = CreateDefaultPatient();
        var newName = new PersonName("Jane", "Smith");
        var newContact = new ContactInfo("+9876543210", "jane@example.com");
        var newAddress = new Address("456 Oak Ave", "Uptown", "Gotham", "State", "67890", "USA");

        // Act
        patient.UpdatePersonalInfo(newName, null, null, newContact, newAddress);

        // Assert
        patient.Name.Should().Be(newName);
        patient.ContactInfo.Should().Be(newContact);
        patient.Address.Should().Be(newAddress);
        patient.Gender.Should().Be(DefaultGender); // unchanged
        patient.DateOfBirth.Should().Be(DefaultDob); // unchanged
        patient.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdatePersonalInfo_WithNewDateOfBirth_ShouldUpdate()
    {
        // Arrange
        var patient = CreateDefaultPatient();
        var newDob = new DateTime(1985, 5, 20);

        // Act
        patient.UpdatePersonalInfo(DefaultName, newDob, null, DefaultContact, DefaultAddress);

        // Assert
        patient.DateOfBirth.Should().Be(newDob);
    }

    [Fact]
    public void UpdatePersonalInfo_WithFutureDateOfBirth_ShouldThrow()
    {
        // Arrange
        var patient = CreateDefaultPatient();
        var futureDob = DateTime.Today.AddDays(1);

        // Act
        var act = () => patient.UpdatePersonalInfo(DefaultName, futureDob, null, DefaultContact, DefaultAddress);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("Patient age cannot be negative.");
    }

    [Fact]
    public void UpdatePersonalInfo_WithNullName_ShouldThrow()
    {
        // Arrange
        var patient = CreateDefaultPatient();

        // Act
        var act = () => patient.UpdatePersonalInfo(null!, null, null, DefaultContact, DefaultAddress);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("name");
    }

    [Fact]
    public void Deactivate_ShouldSetInactiveAndRaiseEvent()
    {
        // Arrange
        var patient = CreateDefaultPatient();

        // Act
        patient.Deactivate();

        // Assert
        patient.IsActive.Should().BeFalse();
        patient.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Reactivate_ShouldSetActive()
    {
        // Arrange
        var patient = CreateDefaultPatient();
        patient.Deactivate();

        // Act
        patient.Reactivate();

        // Assert
        patient.IsActive.Should().BeTrue();
        patient.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateMedicalProfile_WithAllValues_ShouldSetProperties()
    {
        // Arrange
        var patient = CreateDefaultPatient();

        // Act
        patient.UpdateMedicalProfile(BloodType.APositive, Race.Asian, MaritalStatus.Married, "Engineer");

        // Assert
        patient.BloodType.Should().Be(BloodType.APositive);
        patient.Race.Should().Be(Race.Asian);
        patient.MaritalStatus.Should().Be(MaritalStatus.Married);
        patient.Occupation.Should().Be("Engineer");
    }

    [Fact]
    public void UpdateMedicalProfile_WithNullValues_ShouldSetNull()
    {
        // Arrange
        var patient = CreateDefaultPatient();
        patient.UpdateMedicalProfile(BloodType.OPositive, Race.White, MaritalStatus.Single, "Doctor");

        // Act
        patient.UpdateMedicalProfile(null, null, null, null);

        // Assert
        patient.BloodType.Should().BeNull();
        patient.Race.Should().BeNull();
        patient.MaritalStatus.Should().BeNull();
        patient.Occupation.Should().BeNull();
    }

    [Fact]
    public void UpdateInsurance_ShouldSetInsuranceId()
    {
        // Arrange
        var patient = CreateDefaultPatient();

        // Act
        patient.UpdateInsurance("INS-12345");

        // Assert
        patient.InsuranceId.Should().Be("INS-12345");
    }

    [Fact]
    public void UpdateInsurance_WithNull_ShouldSetNull()
    {
        // Arrange
        var patient = CreateDefaultPatient();
        patient.UpdateInsurance("INS-12345");

        // Act
        patient.UpdateInsurance(null);

        // Assert
        patient.InsuranceId.Should().BeNull();
    }

    [Fact]
    public void UpdateEmergencyContact_ShouldSetContactInfo()
    {
        // Arrange
        var patient = CreateDefaultPatient();

        // Act
        patient.UpdateEmergencyContact("Jane Doe", "+1234567890");

        // Assert
        patient.EmergencyContactName.Should().Be("Jane Doe");
        patient.EmergencyContactPhone.Should().Be("+1234567890");
    }

    [Fact]
    public void UpdateNationalId_ShouldSetNationalId()
    {
        // Arrange
        var patient = CreateDefaultPatient();

        // Act
        patient.UpdateNationalId("NID-98765");

        // Assert
        patient.NationalId.Should().Be("NID-98765");
    }

    [Fact]
    public void AddAllergy_WithValidAllergy_ShouldAddToList()
    {
        // Arrange
        var patient = CreateDefaultPatient();
        var allergy = new Allergy("Peanuts", "Hives", "Moderate");

        // Act
        patient.AddAllergy(allergy);

        // Assert
        patient.Allergies.Should().HaveCount(1);
        patient.Allergies.Should().Contain(allergy);
    }

    [Fact]
    public void AddAllergy_WithNull_ShouldThrow()
    {
        // Arrange
        var patient = CreateDefaultPatient();

        // Act
        var act = () => patient.AddAllergy(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("allergy");
    }

    [Fact]
    public void AddCondition_WithValidCondition_ShouldAddToList()
    {
        // Arrange
        var patient = CreateDefaultPatient();
        var condition = new MedicalCondition("Asthma", "J45", DateTime.UtcNow.AddYears(-2), true, "Mild persistent");

        // Act
        patient.AddCondition(condition);

        // Assert
        patient.Conditions.Should().HaveCount(1);
        patient.Conditions.Should().Contain(condition);
    }

    [Fact]
    public void AddCondition_WithNull_ShouldThrow()
    {
        // Arrange
        var patient = CreateDefaultPatient();

        // Act
        var act = () => patient.AddCondition(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("condition");
    }

    [Fact]
    public void MultipleAllergies_ShouldContainAll()
    {
        // Arrange
        var patient = CreateDefaultPatient();
        var a1 = new Allergy("Peanuts", "Hives", "Moderate");
        var a2 = new Allergy("Penicillin", "Rash", "Mild");
        var a3 = new Allergy("Latex", null, null);

        // Act
        patient.AddAllergy(a1);
        patient.AddAllergy(a2);
        patient.AddAllergy(a3);

        // Assert
        patient.Allergies.Should().HaveCount(3);
    }

    [Fact]
    public void RegisterPatient_ShouldRaisePatientRegisteredDomainEvent()
    {
        // Act
        var patient = Patient.Register(DefaultName, DefaultDob, DefaultGender, DefaultContact, DefaultAddress);

        // Assert
        patient.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Events.PatientRegisteredDomainEvent>();
    }

    [Fact]
    public void UpdateGender_ShouldReflectChange()
    {
        // Arrange
        var patient = CreateDefaultPatient();

        // Act
        patient.UpdatePersonalInfo(DefaultName, null, Gender.Female, DefaultContact, DefaultAddress);

        // Assert
        patient.Gender.Should().Be(Gender.Female);
    }
}
