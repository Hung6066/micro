using FluentAssertions;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Events;
using His.Hope.PatientService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.ValueObjects;

namespace His.Hope.PatientService.Domain.Tests;

public class PatientDomainEventTests
{
    private static readonly PersonName DefaultName = new("John", "Doe", "M");
    private static readonly DateTime DefaultDob = new(1990, 1, 15);
    private static readonly Gender DefaultGender = Gender.Male;
    private static readonly ContactInfo DefaultContact = new("+1234567890", "john@example.com");
    private static readonly Address DefaultAddress = new("123 Main St", "Downtown", "Metropolis", "State", "12345", "USA");

    [Fact]
    public void RegisterPatient_RaisesPatientRegisteredDomainEvent()
    {
        // Act
        var patient = Patient.Register(DefaultName, DefaultDob, DefaultGender, DefaultContact, DefaultAddress);

        // Assert
        var domainEvent = patient.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PatientRegisteredDomainEvent>().Subject;

        domainEvent.PatientId.Should().Be(patient.Id.Value);
        domainEvent.FullName.Should().Be(DefaultName.FullName);
        domainEvent.DateOfBirth.Should().Be(DefaultDob);
        domainEvent.GenderCode.Should().Be(DefaultGender.Code);
        domainEvent.Phone.Should().Be(DefaultContact.Phone);
        domainEvent.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdatePersonalInfo_RaisesPatientUpdatedDomainEvent()
    {
        // Arrange
        var patient = Patient.Register(DefaultName, DefaultDob, DefaultGender, DefaultContact, DefaultAddress);
        patient.ClearDomainEvents();
        var newName = new PersonName("Jane", "Smith");
        var newContact = new ContactInfo("+9876543210");
        var newAddress = new Address("456 Oak Ave", "Uptown", "Gotham", "State", "67890", "USA");

        // Act
        patient.UpdatePersonalInfo(newName, null, null, newContact, newAddress);

        // Assert
        var domainEvent = patient.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PatientUpdatedDomainEvent>().Subject;

        domainEvent.PatientId.Should().Be(patient.Id.Value);
        domainEvent.FullName.Should().Be(newName.FullName);
        domainEvent.Phone.Should().Be(newContact.Phone);
    }

    [Fact]
    public void Deactivate_RaisesPatientDeactivatedDomainEvent()
    {
        // Arrange
        var patient = Patient.Register(DefaultName, DefaultDob, DefaultGender, DefaultContact, DefaultAddress);
        patient.ClearDomainEvents();

        // Act
        patient.Deactivate();

        // Assert
        var domainEvent = patient.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PatientDeactivatedDomainEvent>().Subject;

        domainEvent.PatientId.Should().Be(patient.Id.Value);
    }

    [Fact]
    public void Reactivate_RaisesPatientReactivatedDomainEvent()
    {
        // Arrange
        var patient = Patient.Register(DefaultName, DefaultDob, DefaultGender, DefaultContact, DefaultAddress);
        patient.Deactivate();
        patient.ClearDomainEvents();

        // Act
        patient.Reactivate();

        // Assert
        var domainEvent = patient.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PatientReactivatedDomainEvent>().Subject;

        domainEvent.PatientId.Should().Be(patient.Id.Value);
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllEvents()
    {
        // Arrange
        var patient = Patient.Register(DefaultName, DefaultDob, DefaultGender, DefaultContact, DefaultAddress);

        // Act
        patient.ClearDomainEvents();

        // Assert
        patient.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void MultipleOperations_ShouldAccumulateEvents()
    {
        // Arrange
        var patient = Patient.Register(DefaultName, DefaultDob, DefaultGender, DefaultContact, DefaultAddress);
        patient.ClearDomainEvents();

        // Act
        patient.UpdatePersonalInfo(DefaultName, null, null, DefaultContact, DefaultAddress);
        patient.Deactivate();
        patient.Reactivate();

        // Assert
        patient.DomainEvents.Should().HaveCount(3);
        patient.DomainEvents.Should().ContainItemsAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void PatientRegisteredDomainEvent_Constructor_SetsProperties()
    {
        // Arrange
        var patientId = Guid.NewGuid();

        // Act
        var evt = new PatientRegisteredDomainEvent(patientId, "Doe John", DefaultDob, "M", "+1234567890");

        // Assert
        evt.PatientId.Should().Be(patientId);
        evt.FullName.Should().Be("Doe John");
        evt.DateOfBirth.Should().Be(DefaultDob);
        evt.GenderCode.Should().Be("M");
        evt.Phone.Should().Be("+1234567890");
    }

    [Fact]
    public void PatientDeactivatedDomainEvent_Constructor_SetsProperties()
    {
        // Arrange
        var patientId = Guid.NewGuid();

        // Act
        var evt = new PatientDeactivatedDomainEvent(patientId);

        // Assert
        evt.PatientId.Should().Be(patientId);
    }

    [Fact]
    public void PatientUpdatedDomainEvent_Constructor_SetsProperties()
    {
        // Arrange
        var patientId = Guid.NewGuid();

        // Act
        var evt = new PatientUpdatedDomainEvent(patientId, "Doe John", "+1234567890");

        // Assert
        evt.PatientId.Should().Be(patientId);
        evt.FullName.Should().Be("Doe John");
        evt.Phone.Should().Be("+1234567890");
    }

    [Fact]
    public void PatientReactivatedDomainEvent_Constructor_SetsProperties()
    {
        // Arrange
        var patientId = Guid.NewGuid();

        // Act
        var evt = new PatientReactivatedDomainEvent(patientId);

        // Assert
        evt.PatientId.Should().Be(patientId);
    }

    [Fact]
    public void DomainEvents_ShouldHaveOccurredOnTimestamp()
    {
        // Arrange
        var patientId = Guid.NewGuid();

        // Act
        var evt = new PatientRegisteredDomainEvent(patientId, "Doe John", DefaultDob, "M", "+1234567890");

        // Assert
        evt.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
