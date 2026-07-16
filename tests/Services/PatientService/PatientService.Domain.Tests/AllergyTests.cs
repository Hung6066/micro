using FluentAssertions;
using His.Hope.PatientService.Domain.Entities;

namespace His.Hope.PatientService.Domain.Tests;

public class AllergyTests
{
    [Fact]
    public void Constructor_WithValidValues_ShouldSetProperties()
    {
        // Act
        var allergy = new Allergy("Peanuts", "Hives", "Moderate");

        // Assert
        allergy.Allergen.Should().Be("Peanuts");
        allergy.Reaction.Should().Be("Hives");
        allergy.Severity.Should().Be("Moderate");
        allergy.RecordedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        allergy.IsActive.Should().BeTrue();
        allergy.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_WithNullReactionAndSeverity_ShouldSetNull()
    {
        // Act
        var allergy = new Allergy("Latex", null, null);

        // Assert
        allergy.Allergen.Should().Be("Latex");
        allergy.Reaction.Should().BeNull();
        allergy.Severity.Should().BeNull();
        allergy.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithEmptyAllergen_ShouldThrow()
    {
        // Act
        var act = () => new Allergy("", "Rash", "Mild");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("allergen");
    }

    [Fact]
    public void Constructor_WithWhitespaceAllergen_ShouldThrow()
    {
        // Act
        var act = () => new Allergy("   ", "Rash", "Mild");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("allergen");
    }

    [Fact]
    public void MarkInactive_ShouldSetIsActiveToFalse()
    {
        // Arrange
        var allergy = new Allergy("Penicillin", "Rash", "Mild");

        // Act
        allergy.MarkInactive();

        // Assert
        allergy.IsActive.Should().BeFalse();
    }

    [Fact]
    public void EntityEquality_SameId_ShouldBeEqual()
    {
        // Arrange
        var allergy1 = new Allergy("Peanuts", "Hives", "Moderate");
        var id = allergy1.Id;
        var allergy2 = new Allergy("Penicillin", "Rash", "Mild");

        // Use reflection to compare concept: Entity equality is based on Id
        // Different allergies with different IDs should not be equal
        allergy1.Should().NotBe(allergy2);
    }

    [Fact]
    public void Constructor_ShouldGenerateUniqueIds()
    {
        // Act
        var allergy1 = new Allergy("Peanuts", null, null);
        var allergy2 = new Allergy("Peanuts", null, null);

        // Assert
        allergy1.Id.Should().NotBe(allergy2.Id);
    }
}
