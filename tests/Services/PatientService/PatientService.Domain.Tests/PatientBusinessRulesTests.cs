using FluentAssertions;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PatientService.Domain.Tests;

public class PatientBusinessRulesTests
{
    [Fact]
    public void PatientMustBeAtLeastZeroYearsOld_WithNegativeAge_ShouldBeBroken()
    {
        var rule = new PatientMustBeAtLeastZeroYearsOld(-1);
        rule.IsBroken().Should().BeTrue();
        rule.Message.Should().Be("Patient age cannot be negative.");
    }

    [Fact]
    public void PatientMustBeAtLeastZeroYearsOld_WithZeroAge_ShouldNotBeBroken()
    {
        var rule = new PatientMustBeAtLeastZeroYearsOld(0);
        rule.IsBroken().Should().BeFalse();
    }

    [Fact]
    public void PatientMustBeAtLeastZeroYearsOld_WithPositiveAge_ShouldNotBeBroken()
    {
        var rule = new PatientMustBeAtLeastZeroYearsOld(25);
        rule.IsBroken().Should().BeFalse();
    }

    [Fact]
    public void PatientMustBeAtLeastZeroYearsOld_WithLargeAge_ShouldNotBeBroken()
    {
        var rule = new PatientMustBeAtLeastZeroYearsOld(120);
        rule.IsBroken().Should().BeFalse();
    }

    [Fact]
    public void PatientMustBeAtLeastZeroYearsOld_ImplementsIBusinessRule()
    {
        var rule = new PatientMustBeAtLeastZeroYearsOld(30);
        rule.Should().BeAssignableTo<IBusinessRule>();
    }
}
