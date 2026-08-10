using FluentAssertions;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.Exceptions;

namespace His.Hope.SharedKernel.Tests;

public class GuardTests
{
    public class NullOrWhiteSpaceTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NullOrWhiteSpace_WithInvalidInput_ShouldThrow(string? value)
        {
            // Act
            var act = () => Guard.Against.NullOrWhiteSpace(value!, "param");

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithParameterName("param");
        }

        [Fact]
        public void NullOrWhiteSpace_WithValidInput_ShouldReturnValue()
        {
            // Arrange
            const string value = "valid";

            // Act
            var result = Guard.Against.NullOrWhiteSpace(value, "param");

            // Assert
            result.Should().Be("valid");
        }
    }

    public class NullTests
    {
        [Fact]
        public void Null_WithNullValue_ShouldThrow()
        {
            // Act
            var act = () => Guard.Against.Null<object>(null!, "param");

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("param");
        }

        [Fact]
        public void Null_WithNonNullValue_ShouldReturnValue()
        {
            // Arrange
            var value = "test";

            // Act
            var result = Guard.Against.Null(value, "param");

            // Assert
            result.Should().Be("test");
        }

        [Fact]
        public void Null_WithString_ShouldReturnString()
        {
            // Arrange
            var value = "hello";

            // Act
            var result = Guard.Against.Null(value, "param");

            // Assert
            result.Should().Be("hello");
        }
    }

    public class InvalidFormatTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void InvalidFormat_WithNullOrEmpty_ShouldThrow(string? value)
        {
            // Act
            var act = () => Guard.Against.InvalidFormat(value!, @"^\d+$", "param");

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void InvalidFormat_WithNonMatchingPattern_ShouldThrow()
        {
            // Act
            var act = () => Guard.Against.InvalidFormat("abc", @"^\d+$", "param");

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithParameterName("param");
        }

        [Fact]
        public void InvalidFormat_WithMatchingPattern_ShouldReturnValue()
        {
            // Act
            var result = Guard.Against.InvalidFormat("12345", @"^\d+$", "param");

            // Assert
            result.Should().Be("12345");
        }
    }

    public class EmailTests
    {
        [Theory]
        [InlineData("test@example.com")]
        [InlineData("user.name+tag@domain.co.uk")]
        [InlineData("a@b.cd")]
        public void Email_WithValidEmail_ShouldReturnValue(string email)
        {
            // Act
            var result = Guard.Against.Email(email, "param");

            // Assert
            result.Should().Be(email);
        }

        [Theory]
        [InlineData("not-an-email")]
        [InlineData("@domain.com")]
        [InlineData("user@")]
        [InlineData("user@.com")]
        public void Email_WithInvalidEmail_ShouldThrow(string email)
        {
            // Act
            var act = () => Guard.Against.Email(email, "param");

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithParameterName("param");
        }

        [Fact]
        public void Email_WithEmptyString_ShouldThrow()
        {
            // Act
            var act = () => Guard.Against.Email("", "param");

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }

    public class PhoneTests
    {
        [Theory]
        [InlineData("+1234567890")]
        [InlineData("555-123-4567")]
        [InlineData("(555) 123-4567")]
        [InlineData("12345678901234567")]
        public void Phone_WithValidPhone_ShouldReturnValue(string phone)
        {
            // Act
            var result = Guard.Against.Phone(phone, "param");

            // Assert
            result.Should().Be(phone);
        }

        [Theory]
        [InlineData("12")]
        [InlineData("123456789012345678901")]
        [InlineData("")]
        public void Phone_WithInvalidPhone_ShouldThrow(string phone)
        {
            // Act
            var act = () => Guard.Against.Phone(phone, "param");

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }

    public class OutOfRangeTests
    {
        [Fact]
        public void OutOfRange_ValueBelowMin_ShouldThrow()
        {
            // Arrange
            var minDate = new DateTime(2024, 1, 1);

            // Act
            var act = () => Guard.Against.OutOfRange(
                new DateTime(2023, 12, 31), "param", min: minDate);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("param");
        }

        [Fact]
        public void OutOfRange_ValueAboveMax_ShouldThrow()
        {
            // Arrange
            var maxDate = new DateTime(2024, 12, 31);

            // Act
            var act = () => Guard.Against.OutOfRange(
                new DateTime(2025, 1, 1), "param", max: maxDate);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>()
                .WithParameterName("param");
        }

        [Fact]
        public void OutOfRange_ValueWithinRange_ShouldReturnValue()
        {
            // Arrange
            var value = new DateTime(2024, 6, 15);
            var minDate = new DateTime(2024, 1, 1);
            var maxDate = new DateTime(2024, 12, 31);

            // Act
            var result = Guard.Against.OutOfRange(value, "param", min: minDate, max: maxDate);

            // Assert
            result.Should().Be(value);
        }

        [Fact]
        public void OutOfRange_WithNoConstraints_ShouldReturnValue()
        {
            // Arrange
            var value = new DateTime(2024, 6, 15);

            // Act
            var result = Guard.Against.OutOfRange(value, "param");

            // Assert
            result.Should().Be(value);
        }
    }

    public class BusinessRuleTests
    {
        [Fact]
        public void BusinessRule_WhenBroken_ShouldThrowDomainException()
        {
            // Arrange
            var rule = new TestBusinessRule(true);

            // Act
            var act = () => Guard.Against.BusinessRule(rule);

            // Assert
            act.Should().Throw<DomainException>()
                .WithMessage("Business rule broken");
        }

        [Fact]
        public void BusinessRule_WhenNotBroken_ShouldNotThrow()
        {
            // Arrange
            var rule = new TestBusinessRule(false);

            // Act
            var act = () => Guard.Against.BusinessRule(rule);

            // Assert
            act.Should().NotThrow();
        }

        private class TestBusinessRule : IBusinessRule
        {
            private readonly bool _isBroken;
            public TestBusinessRule(bool isBroken) => _isBroken = isBroken;
            public bool IsBroken() => _isBroken;
            public string Message => "Business rule broken";
        }
    }
}
