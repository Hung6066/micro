using FluentAssertions;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.SharedKernel.Tests;

public class EnumerationTests
{
    private class TestEnumeration : Enumeration<TestEnumeration>
    {
        public static readonly TestEnumeration OptionA = new("A", "Option A");
        public static readonly TestEnumeration OptionB = new("B", "Option B");
        public static readonly TestEnumeration OptionC = new("C", "Option C");

        private TestEnumeration(string code, string name) : base(code, name) { }
    }

    [Fact]
    public void GetAll_ShouldReturnAllOptions()
    {
        // Act
        var all = TestEnumeration.GetAll();

        // Assert
        all.Should().HaveCount(3);
        all.Should().Contain(TestEnumeration.OptionA);
        all.Should().Contain(TestEnumeration.OptionB);
        all.Should().Contain(TestEnumeration.OptionC);
    }

    [Fact]
    public void FromCode_WithValidCode_ShouldReturnCorrectOption()
    {
        // Act
        var option = TestEnumeration.FromCode("B");

        // Assert
        option.Should().Be(TestEnumeration.OptionB);
    }

    [Fact]
    public void FromCode_WithInvalidCode_ShouldThrow()
    {
        // Act
        var act = () => TestEnumeration.FromCode("INVALID");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*INVALID*TestEnumeration*");
    }

    [Fact]
    public void FromName_WithValidName_ShouldReturnCorrectOption()
    {
        // Act
        var option = TestEnumeration.FromName("Option C");

        // Assert
        option.Should().Be(TestEnumeration.OptionC);
    }

    [Fact]
    public void FromName_WithInvalidName_ShouldThrow()
    {
        // Act
        var act = () => TestEnumeration.FromName("Invalid Option");

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Equality_SameCode_ShouldBeEqual()
    {
        // Arrange
        var opt1 = TestEnumeration.FromCode("A");
        var opt2 = TestEnumeration.FromCode("A");

        // Act & Assert
        opt1.Should().Be(opt2);
        (opt1 == opt2).Should().BeTrue();
        opt1.GetHashCode().Should().Be(opt2.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentCodes_ShouldNotBeEqual()
    {
        // Arrange
        var opt1 = TestEnumeration.OptionA;
        var opt2 = TestEnumeration.OptionB;

        // Act & Assert
        opt1.Should().NotBe(opt2);
        (opt1 != opt2).Should().BeTrue();
    }

    [Fact]
    public void CompareTo_SameOption_ShouldReturnZero()
    {
        // Arrange
        var opt1 = TestEnumeration.OptionA;
        var opt2 = TestEnumeration.OptionA;

        // Act
        var result = opt1.CompareTo(opt2);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CompareTo_DifferentOptions_ShouldReturnNonZero()
    {
        // Arrange
        var optA = TestEnumeration.OptionA;
        var optB = TestEnumeration.OptionB;

        // Act
        var result = optA.CompareTo(optB);

        // Assert
        result.Should().NotBe(0);
    }

    [Fact]
    public void Constructor_WithNullCode_ShouldThrow()
    {
        // Act
        var act = () => new TestEnumerationPrivate(null!, "Name");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullName_ShouldThrow()
    {
        // Act
        var act = () => new TestEnumerationPrivate("C", null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyCode_ShouldThrow()
    {
        // Act
        var act = () => new TestEnumerationPrivate("", "Name");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithWhitespaceName_ShouldThrow()
    {
        // Act
        var act = () => new TestEnumerationPrivate("C", "   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Code_ShouldBeAccessible()
    {
        // Assert
        TestEnumeration.OptionA.Code.Should().Be("A");
        TestEnumeration.OptionB.Code.Should().Be("B");
    }

    [Fact]
    public void Name_ShouldBeAccessible()
    {
        // Assert
        TestEnumeration.OptionA.Name.Should().Be("Option A");
        TestEnumeration.OptionB.Name.Should().Be("Option B");
    }

    [Fact]
    public void GetAll_ShouldReturnReadOnlyCollection()
    {
        // Act
        var all = TestEnumeration.GetAll();

        // Assert
        all.Should().BeAssignableTo<IReadOnlyCollection<TestEnumeration>>();
    }

    [Fact]
    public void Equals_WithNull_ShouldReturnFalse()
    {
        // Arrange
        var opt = TestEnumeration.OptionA;

        // Act
        var result = opt.Equals(null);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Helper class to test constructor validation.
    /// </summary>
    private class TestEnumerationPrivate : Enumeration<TestEnumerationPrivate>
    {
        public TestEnumerationPrivate(string code, string name) : base(code, name) { }
    }
}
