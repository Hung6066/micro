using FluentAssertions;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.SharedKernel.Tests;

public class ValueObjectEqualityTests
{
    private class TestValueObject : ValueObject
    {
        public string PropertyA { get; }
        public int PropertyB { get; }
        public string? PropertyC { get; }

        public TestValueObject(string propertyA, int propertyB, string? propertyC = null)
        {
            PropertyA = propertyA;
            PropertyB = propertyB;
            PropertyC = propertyC;
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return PropertyA;
            yield return PropertyB;
            yield return PropertyC ?? string.Empty;
        }
    }

    private class DerivedValueObject : TestValueObject
    {
        public string ExtraProperty { get; }

        public DerivedValueObject(string propertyA, int propertyB, string extraProperty, string? propertyC = null)
            : base(propertyA, propertyB, propertyC)
        {
            ExtraProperty = extraProperty;
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            foreach (var component in base.GetEqualityComponents())
                yield return component;
            yield return ExtraProperty;
        }
    }

    [Fact]
    public void SameValues_ShouldBeEqual()
    {
        // Arrange
        var vo1 = new TestValueObject("test", 42, "optional");
        var vo2 = new TestValueObject("test", 42, "optional");

        // Act & Assert
        vo1.Should().Be(vo2);
        (vo1 == vo2).Should().BeTrue();
        vo1.Equals(vo2).Should().BeTrue();
        vo1.Equals((object)vo2).Should().BeTrue();
    }

    [Fact]
    public void SameValues_ShouldHaveSameHashCode()
    {
        // Arrange
        var vo1 = new TestValueObject("test", 42, "optional");
        var vo2 = new TestValueObject("test", 42, "optional");

        // Act & Assert
        vo1.GetHashCode().Should().Be(vo2.GetHashCode());
    }

    [Fact]
    public void DifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var vo1 = new TestValueObject("test", 42);
        var vo2 = new TestValueObject("different", 42);

        // Act & Assert
        vo1.Should().NotBe(vo2);
        (vo1 != vo2).Should().BeTrue();
    }

    [Fact]
    public void DifferentIntValues_ShouldNotBeEqual()
    {
        // Arrange
        var vo1 = new TestValueObject("test", 42);
        var vo2 = new TestValueObject("test", 99);

        // Act & Assert
        vo1.Should().NotBe(vo2);
    }

    [Fact]
    public void NullVsNonNullOptional_ShouldNotBeEqual()
    {
        // Arrange
        var vo1 = new TestValueObject("test", 42, null);
        var vo2 = new TestValueObject("test", 42, "value");

        // Act & Assert
        vo1.Should().NotBe(vo2);
    }

    [Fact]
    public void BothNullOptional_ShouldBeEqual()
    {
        // Arrange
        var vo1 = new TestValueObject("test", 42, null);
        var vo2 = new TestValueObject("test", 42, null);

        // Act & Assert
        vo1.Should().Be(vo2);
    }

    [Fact]
    public void NullComparedToNull_OperatorShouldWork()
    {
        // Arrange
        TestValueObject? vo1 = null;
        TestValueObject? vo2 = null;

        // Act & Assert
        (vo1 == vo2).Should().BeTrue();
        (vo1 != vo2).Should().BeFalse();
    }

    [Fact]
    public void ValueObjectComparedToNull_ShouldNotBeEqual()
    {
        // Arrange
        var vo = new TestValueObject("test", 42);

        // Act & Assert
        vo.Equals(null).Should().BeFalse();
        (vo == null!).Should().BeFalse();
        (null! == vo).Should().BeFalse();
    }

    [Fact]
    public void DifferentTypes_ShouldNotBeEqual()
    {
        // Arrange
        var vo1 = new TestValueObject("test", 42);
        var vo2 = new DerivedValueObject("test", 42, "extra");

        // Act & Assert
        vo1.Should().NotBe(vo2);
        vo1.Equals(vo2).Should().BeFalse();
    }

    [Fact]
    public void DerivedType_SameValues_ShouldBeEqual()
    {
        // Arrange
        var vo1 = new DerivedValueObject("test", 42, "extra");
        var vo2 = new DerivedValueObject("test", 42, "extra");

        // Act & Assert
        vo1.Should().Be(vo2);
        vo1.GetHashCode().Should().Be(vo2.GetHashCode());
    }

    [Fact]
    public void DifferentOrderOfComponents_ShouldStillBeEqual()
    {
        // ValueObjects define equality by the ordered sequence of components
        var vo1 = new TestValueObject("first", 1, "second");
        var vo2 = new TestValueObject("first", 1, "second");

        vo1.Should().Be(vo2);
    }

    [Fact]
    public void EmptyVsNonEmptyString_ShouldNotBeEqual()
    {
        // The GetEqualityComponents uses ?? string.Empty for nulls
        // An empty string and null will be equal
        var vo1 = new TestValueObject("test", 1, "");
        var vo2 = new TestValueObject("test", 1, null);

        vo1.Should().Be(vo2);
    }
}
