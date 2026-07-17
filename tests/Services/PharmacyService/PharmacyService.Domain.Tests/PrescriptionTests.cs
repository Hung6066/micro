using FluentAssertions;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.Events;
using His.Hope.PharmacyService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Exceptions;

namespace His.Hope.PharmacyService.Domain.Tests;

public class PrescriptionTests
{
    private static readonly Guid DefaultPatientId = Guid.NewGuid();
    private static readonly Guid DefaultProviderId = Guid.NewGuid();
    private const string DefaultMedicationName = "Amoxicillin";
    private const string DefaultStrength = "500mg";
    private const string DefaultDosageForm = "Capsule";
    private const string DefaultDosageInstructions = "Take one capsule three times daily";
    private const int DefaultQuantity = 30;
    private const int DefaultRefills = 2;

    private Prescription CreateDefaultPrescription()
    {
        return Prescription.Create(
            DefaultPatientId,
            DefaultProviderId,
            null,
            DefaultMedicationName,
            DefaultStrength,
            DefaultDosageForm,
            DefaultDosageInstructions,
            "Oral",
            DefaultQuantity,
            DefaultRefills,
            null,
            null);
    }

    [Fact]
    public void Create_WithValidParameters_ShouldCreatePrescribedPrescription()
    {
        var prescription = CreateDefaultPrescription();

        prescription.Should().NotBeNull();
        prescription.PatientId.Should().Be(DefaultPatientId);
        prescription.ProviderId.Should().Be(DefaultProviderId);
        prescription.MedicationName.Should().Be(DefaultMedicationName);
        prescription.Strength.Should().Be(DefaultStrength);
        prescription.DosageForm.Should().Be(DefaultDosageForm);
        prescription.DosageInstructions.Should().Be(DefaultDosageInstructions);
        prescription.Quantity.Should().Be(DefaultQuantity);
        prescription.Refills.Should().Be(DefaultRefills);
        prescription.Status.Should().Be(PrescriptionStatus.Prescribed);
        prescription.PrescribedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        prescription.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        prescription.FilledDate.Should().BeNull();
        prescription.CancelledDate.Should().BeNull();
        prescription.CancellationReason.Should().BeNull();
        prescription.Notes.Should().BeNull();
    }

    [Fact]
    public void Create_WithNullMedicationName_ShouldThrow()
    {
        var act = () => Prescription.Create(
            DefaultPatientId, DefaultProviderId, null, null!,
            DefaultStrength, DefaultDosageForm, DefaultDosageInstructions,
            null, DefaultQuantity, DefaultRefills, null, null);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("medicationName");
    }

    [Fact]
    public void Create_WithEmptyMedicationName_ShouldThrow()
    {
        var act = () => Prescription.Create(
            DefaultPatientId, DefaultProviderId, null, "",
            DefaultStrength, DefaultDosageForm, DefaultDosageInstructions,
            null, DefaultQuantity, DefaultRefills, null, null);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("medicationName");
    }

    [Fact]
    public void Create_WithZeroQuantity_ShouldThrow()
    {
        var act = () => Prescription.Create(
            DefaultPatientId, DefaultProviderId, null,
            DefaultMedicationName, DefaultStrength, DefaultDosageForm,
            DefaultDosageInstructions, null, 0, DefaultRefills, null, null);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Quantity must be greater than zero*");
    }

    [Fact]
    public void Create_WithNegativeQuantity_ShouldThrow()
    {
        var act = () => Prescription.Create(
            DefaultPatientId, DefaultProviderId, null,
            DefaultMedicationName, DefaultStrength, DefaultDosageForm,
            DefaultDosageInstructions, null, -1, DefaultRefills, null, null);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Quantity must be greater than zero*");
    }

    [Fact]
    public void Fill_ShouldTransitionToFilled()
    {
        var prescription = CreateDefaultPrescription();

        prescription.Fill();

        prescription.Status.Should().Be(PrescriptionStatus.Filled);
        prescription.FilledDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        prescription.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Fill_WhenAlreadyFilled_ShouldThrow()
    {
        var prescription = CreateDefaultPrescription();
        prescription.Fill();

        var act = () => prescription.Fill();

        act.Should().Throw<DomainException>()
            .WithMessage("Prescription has already been filled.");
    }

    [Fact]
    public void Cancel_ShouldTransitionToCancelled()
    {
        var prescription = CreateDefaultPrescription();
        const string reason = "Patient allergic";

        prescription.Cancel(reason);

        prescription.Status.Should().Be(PrescriptionStatus.Cancelled);
        prescription.CancellationReason.Should().Be(reason);
        prescription.CancelledDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        prescription.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ShouldThrow()
    {
        var prescription = CreateDefaultPrescription();
        prescription.Cancel("Reason");

        var act = () => prescription.Cancel("Another reason");

        act.Should().Throw<DomainException>()
            .WithMessage("Prescription has already been cancelled.");
    }

    [Fact]
    public void Cancel_WhenFilled_ShouldThrow()
    {
        var prescription = CreateDefaultPrescription();
        prescription.Fill();

        var act = () => prescription.Cancel("Reason");

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot cancel a filled prescription.");
    }

    [Fact]
    public void Cancel_WithEmptyReason_ShouldThrow()
    {
        var prescription = CreateDefaultPrescription();

        var act = () => prescription.Cancel("");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("reason");
    }

    [Fact]
    public void Create_ShouldRaisePrescriptionCreatedDomainEvent()
    {
        var prescription = CreateDefaultPrescription();

        prescription.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PrescriptionCreatedDomainEvent>();
    }

    [Fact]
    public void Fill_ShouldRaisePrescriptionFilledDomainEvent()
    {
        var prescription = CreateDefaultPrescription();

        prescription.ClearDomainEvents();
        prescription.Fill();

        prescription.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PrescriptionFilledDomainEvent>();
    }

    [Fact]
    public void Cancel_ShouldRaisePrescriptionCancelledDomainEvent()
    {
        var prescription = CreateDefaultPrescription();

        prescription.ClearDomainEvents();
        prescription.Cancel("Reason");

        prescription.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PrescriptionCancelledDomainEvent>();
    }

    [Fact]
    public void PrescriptionId_ShouldBeGenerated()
    {
        var prescription = CreateDefaultPrescription();

        prescription.Id.Should().NotBeNull();
        prescription.Id.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void MultiplePrescriptions_ShouldHaveDifferentIds()
    {
        var p1 = CreateDefaultPrescription();
        var p2 = CreateDefaultPrescription();

        p1.Id.Value.Should().NotBe(p2.Id.Value);
    }
}
