using FluentAssertions;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.Events;

namespace His.Hope.PharmacyService.Domain.Tests;

public class MedicationTests
{
    private const string DefaultName = "Amoxicillin";
    private const string DefaultDosageForm = "Capsule";
    private const string DefaultStrength = "500mg";

    private Medication CreateDefaultMedication()
    {
        return Medication.Create(DefaultName, DefaultDosageForm, DefaultStrength);
    }

    [Fact]
    public void Create_WithValidParameters_ShouldCreateActiveMedication()
    {
        var medication = CreateDefaultMedication();

        medication.Should().NotBeNull();
        medication.Name.Should().Be(DefaultName);
        medication.DosageForm.Should().Be(DefaultDosageForm);
        medication.Strength.Should().Be(DefaultStrength);
        medication.IsActive.Should().BeTrue();
        medication.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        medication.GenericName.Should().BeNull();
        medication.BrandName.Should().BeNull();
        medication.Route.Should().BeNull();
        medication.Category.Should().BeNull();
        medication.Manufacturer.Should().BeNull();
        medication.RequiresPrescription.Should().BeFalse();
    }

    [Fact]
    public void Create_WithNullName_ShouldThrow()
    {
        var act = () => Medication.Create(null!, DefaultDosageForm, DefaultStrength);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Fact]
    public void Create_WithEmptyName_ShouldThrow()
    {
        var act = () => Medication.Create("", DefaultDosageForm, DefaultStrength);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Fact]
    public void Create_WithNullDosageForm_ShouldThrow()
    {
        var act = () => Medication.Create(DefaultName, null!, DefaultStrength);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("dosageForm");
    }

    [Fact]
    public void Create_WithNullStrength_ShouldThrow()
    {
        var act = () => Medication.Create(DefaultName, DefaultDosageForm, null!);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("strength");
    }

    [Fact]
    public void UpdateDetails_WithAllValues_ShouldUpdateAllFields()
    {
        var medication = CreateDefaultMedication();

        medication.UpdateDetails(
            "Updated Name",
            "GenericName",
            "BrandName",
            "Tablet",
            "250mg",
            "Oral",
            "Antibiotic",
            "PharmaCorp",
            true);

        medication.Name.Should().Be("Updated Name");
        medication.GenericName.Should().Be("GenericName");
        medication.BrandName.Should().Be("BrandName");
        medication.DosageForm.Should().Be("Tablet");
        medication.Strength.Should().Be("250mg");
        medication.Route.Should().Be("Oral");
        medication.Category.Should().Be("Antibiotic");
        medication.Manufacturer.Should().Be("PharmaCorp");
        medication.RequiresPrescription.Should().BeTrue();
        medication.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateDetails_WithNullOptionals_ShouldSetNull()
    {
        var medication = CreateDefaultMedication();
        medication.UpdateDetails(DefaultName, "Gen", "Brand", DefaultDosageForm, DefaultStrength, "Oral", "Cat", "Mfr", true);

        medication.UpdateDetails(DefaultName, null, null, DefaultDosageForm, DefaultStrength, null, null, null, false);

        medication.GenericName.Should().BeNull();
        medication.BrandName.Should().BeNull();
        medication.Route.Should().BeNull();
        medication.Category.Should().BeNull();
        medication.Manufacturer.Should().BeNull();
        medication.RequiresPrescription.Should().BeFalse();
    }

    [Fact]
    public void UpdateDetails_WithEmptyName_ShouldThrow()
    {
        var medication = CreateDefaultMedication();

        var act = () => medication.UpdateDetails("", null, null, DefaultDosageForm, DefaultStrength, null, null, null, false);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var medication = CreateDefaultMedication();

        medication.Deactivate();

        medication.IsActive.Should().BeFalse();
        medication.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldRemainInactive()
    {
        var medication = CreateDefaultMedication();
        medication.Deactivate();

        medication.Deactivate();

        medication.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Reactivate_ShouldSetIsActiveTrue()
    {
        var medication = CreateDefaultMedication();
        medication.Deactivate();

        medication.Reactivate();

        medication.IsActive.Should().BeTrue();
        medication.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Reactivate_WhenAlreadyActive_ShouldRemainActive()
    {
        var medication = CreateDefaultMedication();

        medication.Reactivate();

        medication.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldRaiseMedicationCreatedDomainEvent()
    {
        var medication = CreateDefaultMedication();

        medication.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MedicationCreatedDomainEvent>();
    }

    [Fact]
    public void UpdateDetails_ShouldRaiseMedicationUpdatedDomainEvent()
    {
        var medication = CreateDefaultMedication();
        medication.ClearDomainEvents();

        medication.UpdateDetails("Updated", null, null, DefaultDosageForm, DefaultStrength, null, null, null, false);

        medication.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MedicationUpdatedDomainEvent>();
    }

    [Fact]
    public void Deactivate_ShouldRaiseMedicationDeactivatedDomainEvent()
    {
        var medication = CreateDefaultMedication();
        medication.ClearDomainEvents();

        medication.Deactivate();

        medication.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MedicationDeactivatedDomainEvent>();
    }

    [Fact]
    public void Reactivate_ShouldRaiseMedicationReactivatedDomainEvent()
    {
        var medication = CreateDefaultMedication();
        medication.Deactivate();
        medication.ClearDomainEvents();

        medication.Reactivate();

        medication.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MedicationReactivatedDomainEvent>();
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldNotRaiseEvent()
    {
        var medication = CreateDefaultMedication();
        medication.Deactivate();
        medication.ClearDomainEvents();

        medication.Deactivate();

        medication.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Reactivate_WhenAlreadyActive_ShouldNotRaiseEvent()
    {
        var medication = CreateDefaultMedication();
        medication.ClearDomainEvents();

        medication.Reactivate();

        medication.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void MedicationId_ShouldBeGenerated()
    {
        var medication = CreateDefaultMedication();

        medication.Id.Should().NotBeNull();
        medication.Id.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void MultipleMedications_ShouldHaveDifferentIds()
    {
        var m1 = CreateDefaultMedication();
        var m2 = CreateDefaultMedication();

        m1.Id.Value.Should().NotBe(m2.Id.Value);
    }
}
