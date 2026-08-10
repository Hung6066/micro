using FluentAssertions;
using His.Hope.PharmacyService.Domain.Events;

namespace His.Hope.PharmacyService.Domain.Tests;

public class DomainEventTests
{
    [Fact]
    public void PrescriptionCreatedDomainEvent_ShouldSetProperties()
    {
        var prescriptionId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        var @event = new PrescriptionCreatedDomainEvent(
            prescriptionId, patientId, providerId, "Amoxicillin", 30);

        @event.PrescriptionId.Should().Be(prescriptionId);
        @event.PatientId.Should().Be(patientId);
        @event.ProviderId.Should().Be(providerId);
        @event.MedicationName.Should().Be("Amoxicillin");
        @event.Quantity.Should().Be(30);
        @event.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PrescriptionFilledDomainEvent_ShouldSetProperties()
    {
        var prescriptionId = Guid.NewGuid();
        var filledDate = DateTime.UtcNow;

        var @event = new PrescriptionFilledDomainEvent(prescriptionId, filledDate);

        @event.PrescriptionId.Should().Be(prescriptionId);
        @event.FilledDate.Should().Be(filledDate);
        @event.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PrescriptionCancelledDomainEvent_ShouldSetProperties()
    {
        var prescriptionId = Guid.NewGuid();

        var @event = new PrescriptionCancelledDomainEvent(prescriptionId, "Patient request");

        @event.PrescriptionId.Should().Be(prescriptionId);
        @event.Reason.Should().Be("Patient request");
        @event.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MedicationCreatedDomainEvent_ShouldSetProperties()
    {
        var medicationId = Guid.NewGuid();

        var @event = new MedicationCreatedDomainEvent(medicationId, "Amoxicillin", "Capsule", "500mg");

        @event.MedicationId.Should().Be(medicationId);
        @event.Name.Should().Be("Amoxicillin");
        @event.DosageForm.Should().Be("Capsule");
        @event.Strength.Should().Be("500mg");
        @event.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MedicationUpdatedDomainEvent_ShouldSetProperties()
    {
        var medicationId = Guid.NewGuid();

        var @event = new MedicationUpdatedDomainEvent(medicationId, "Amoxicillin");

        @event.MedicationId.Should().Be(medicationId);
        @event.Name.Should().Be("Amoxicillin");
        @event.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MedicationDeactivatedDomainEvent_ShouldSetProperties()
    {
        var medicationId = Guid.NewGuid();

        var @event = new MedicationDeactivatedDomainEvent(medicationId);

        @event.MedicationId.Should().Be(medicationId);
        @event.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MedicationReactivatedDomainEvent_ShouldSetProperties()
    {
        var medicationId = Guid.NewGuid();

        var @event = new MedicationReactivatedDomainEvent(medicationId);

        @event.MedicationId.Should().Be(medicationId);
        @event.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
