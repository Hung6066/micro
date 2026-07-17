using FluentAssertions;
using His.Hope.SharedKernel.Domain.ValueObjects;

namespace His.Hope.SharedKernel.Tests;

public class PersonNameValueObjectTests
{
    [Fact]
    public void Constructor_WithFirstAndLastName_ShouldSetProperties()
    {
        var name = new PersonName("John", "Doe");
        name.FirstName.Should().Be("John");
        name.LastName.Should().Be("Doe");
        name.MiddleName.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithMiddleName_ShouldSetMiddleName()
    {
        var name = new PersonName("John", "Doe", "M");
        name.MiddleName.Should().Be("M");
    }

    [Fact]
    public void Constructor_WithNullFirstName_ShouldThrow()
    {
        var act = () => new PersonName(null!, "Doe");
        act.Should().Throw<ArgumentException>()
            .WithParameterName("firstName");
    }

    [Fact]
    public void Constructor_WithEmptyFirstName_ShouldThrow()
    {
        var act = () => new PersonName("", "Doe");
        act.Should().Throw<ArgumentException>()
            .WithParameterName("firstName");
    }

    [Fact]
    public void Constructor_WithNullLastName_ShouldThrow()
    {
        var act = () => new PersonName("John", null!);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("lastName");
    }

    [Fact]
    public void FullName_WithoutMiddleName_ShouldBeLastNameSpaceFirstName()
    {
        var name = new PersonName("John", "Doe");
        name.FullName.Should().Be("Doe John");
    }

    [Fact]
    public void FullName_WithMiddleName_ShouldIncludeMiddleName()
    {
        var name = new PersonName("John", "Doe", "M");
        name.FullName.Should().Be("Doe M John");
    }

    [Fact]
    public void FullName_WithEmptyMiddleName_ShouldExcludeMiddleName()
    {
        var name = new PersonName("John", "Doe", "");
        name.FullName.Should().Be("Doe John");
    }

    [Fact]
    public void FullName_WithWhitespaceMiddleName_ShouldExcludeMiddleName()
    {
        var name = new PersonName("John", "Doe", "   ");
        name.FullName.Should().Be("Doe John");
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var name1 = new PersonName("John", "Doe", "M");
        var name2 = new PersonName("John", "Doe", "M");
        name1.Should().Be(name2);
        name1.GetHashCode().Should().Be(name2.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentFirstNames_ShouldNotBeEqual()
    {
        var name1 = new PersonName("John", "Doe");
        var name2 = new PersonName("Jane", "Doe");
        name1.Should().NotBe(name2);
    }

    [Fact]
    public void Equality_DifferentLastNames_ShouldNotBeEqual()
    {
        var name1 = new PersonName("John", "Doe");
        var name2 = new PersonName("John", "Smith");
        name1.Should().NotBe(name2);
    }

    [Fact]
    public void Equality_DifferentMiddleNames_ShouldNotBeEqual()
    {
        var name1 = new PersonName("John", "Doe", "M");
        var name2 = new PersonName("John", "Doe", "A");
        name1.Should().NotBe(name2);
    }

    [Fact]
    public void Equality_NullVsEmptyMiddleName_ShouldBeEqual()
    {
        var name1 = new PersonName("John", "Doe", null);
        var name2 = new PersonName("John", "Doe", "");
        name1.Should().Be(name2);
    }

    [Fact]
    public void OperatorOverloads_ShouldWork()
    {
        var name1 = new PersonName("John", "Doe");
        var name2 = new PersonName("John", "Doe");
        var name3 = new PersonName("Jane", "Doe");

        (name1 == name2).Should().BeTrue();
        (name1 != name3).Should().BeTrue();
    }


}
