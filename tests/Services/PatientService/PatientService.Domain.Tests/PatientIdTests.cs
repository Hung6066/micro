using FluentAssertions;
using His.Hope.PatientService.Domain.Entities;

namespace His.Hope.PatientService.Domain.Tests;

public class PatientIdTests
{
    [Fact]
    public void Constructor_WithValidGuid_ShouldCreate()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = new PatientId(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void Constructor_WithEmptyGuid_ShouldThrow()
    {
        // Act
        var act = () => new PatientId(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("value")
            .WithMessage("PatientId cannot be empty*");
    }

    [Fact]
    public void New_ShouldGenerateNonEmptyGuid()
    {
        // Act
        var id = PatientId.New();

        // Assert
        id.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void From_WithValidGuid_ShouldCreate()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = PatientId.From(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void Equality_SameValue_ShouldBeEqual()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new PatientId(guid);
        var id2 = new PatientId(guid);

        // Act & Assert
        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var id1 = new PatientId(Guid.NewGuid());
        var id2 = new PatientId(Guid.NewGuid());

        // Act & Assert
        id1.Should().NotBe(id2);
        (id1 != id2).Should().BeTrue();
    }

    [Fact]
    public void ToString_ShouldReturnGuidString()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = new PatientId(guid);

        // Act
        var str = id.ToString();

        // Assert
        str.Should().Be(guid.ToString());
    }
}
