using FluentAssertions;
using His.Hope.SharedKernel.Domain.Exceptions;

namespace His.Hope.SharedKernel.Tests;

public class DomainExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_ShouldSetMessage()
    {
        var ex = new DomainException("Test error");
        ex.Message.Should().Be("Test error");
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_ShouldSetBoth()
    {
        var inner = new InvalidOperationException("Inner");
        var ex = new DomainException("Outer", inner);

        ex.Message.Should().Be("Outer");
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void DomainException_ShouldBeExceptionType()
    {
        var ex = new DomainException("test");
        ex.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void ThrowAndCatch_ShouldWork()
    {
        Action act = () => throw new DomainException("Something broke");
        act.Should().Throw<DomainException>()
            .WithMessage("Something broke");
    }
}
