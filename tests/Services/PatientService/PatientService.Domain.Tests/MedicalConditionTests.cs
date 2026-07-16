using FluentAssertions;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.SharedKernel.Domain.ValueObjects;

namespace His.Hope.PatientService.Domain.Tests;

public class MedicalConditionTests
{
    [Fact]
    public void Constructor_WithValidValues_ShouldSetProperties()
    {
        // Arrange
        var onsetDate = new DateTime(2023, 1, 15);

        // Act
        var condition = new MedicalCondition("Asthma", "J45", onsetDate, true, "Mild persistent");

        // Assert
        condition.ConditionName.Should().Be("Asthma");
        condition.Icd10Code.Should().Be("J45");
        condition.OnsetDate.Should().Be(onsetDate);
        condition.IsChronic.Should().BeTrue();
        condition.Notes.Should().Be("Mild persistent");
        condition.RecordedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        condition.IsActive.Should().BeTrue();
        condition.ResolvedDate.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithAcuteCondition_ShouldNotBeChronic()
    {
        // Act
        var condition = new MedicalCondition("Acute Bronchitis", "J20", DateTime.UtcNow.AddDays(-5), false, null);

        // Assert
        condition.IsChronic.Should().BeFalse();
        condition.Notes.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullIcd10CodeAndNotes_ShouldAllowNull()
    {
        // Act
        var condition = new MedicalCondition("Common Cold", null, null, false, null);

        // Assert
        condition.Icd10Code.Should().BeNull();
        condition.OnsetDate.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithEmptyConditionName_ShouldThrow()
    {
        // Act
        var act = () => new MedicalCondition("", null, null, false, null);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("conditionName");
    }

    [Fact]
    public void Constructor_WithWhitespaceConditionName_ShouldThrow()
    {
        // Act
        var act = () => new MedicalCondition("   ", "J45", null, true, null);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("conditionName");
    }

    [Fact]
    public void Resolve_WithResolvedDate_ShouldUpdateStatus()
    {
        // Arrange
        var condition = new MedicalCondition("Asthma", "J45", DateTime.UtcNow.AddYears(-5), true, null);
        var resolvedDate = DateTime.UtcNow;

        // Act
        condition.Resolve(resolvedDate);

        // Assert
        condition.IsActive.Should().BeFalse();
        condition.ResolvedDate.Should().Be(resolvedDate);
    }

    [Fact]
    public void EntityEquality_SameId_ShouldBeEqual()
    {
        // This test verifies that different conditions have different IDs
        // and therefore are not equal by entity identity
        var c1 = new MedicalCondition("Asthma", "J45", null, true, null);
        var c2 = new MedicalCondition("Asthma", "J45", null, true, null);

        c1.Should().NotBe(c2);
    }

    [Fact]
    public void Constructor_ShouldGenerateUniqueIds()
    {
        // Act
        var c1 = new MedicalCondition("Asthma", "J45", null, true, null);
        var c2 = new MedicalCondition("Diabetes", "E11", null, true, null);

        // Assert
        c1.Id.Should().NotBe(c2.Id);
    }
}
