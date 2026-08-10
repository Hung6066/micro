using FluentAssertions;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Exceptions;

namespace His.Hope.LabService.Domain.Tests;

public class CriticalAlertRuleTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateActiveRule()
    {
        var rule = CriticalAlertRule.Create(
            "CBC",
            "Complete Blood Count",
            "x10^9/L",
            1.0m,
            10.0m,
            "system",
            "System");

        rule.TestCode.Should().Be("CBC");
        rule.TestName.Should().Be("Complete Blood Count");
        rule.Unit.Should().Be("x10^9/L");
        rule.LowCriticalValue.Should().Be(1.0m);
        rule.HighCriticalValue.Should().Be(10.0m);
        rule.IsActive.Should().BeTrue();
        rule.CreatedByUserId.Should().Be("system");
        rule.CreatedByDisplayName.Should().Be("System");
        rule.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Matches_WithValueOutsideThreshold_ShouldReturnTrue()
    {
        var rule = CriticalAlertRule.Create(
            "CBC",
            "Complete Blood Count",
            "x10^9/L",
            1.0m,
            10.0m,
            "system",
            "System");

        rule.Matches("x10^9/L", 12.5m).Should().BeTrue();
        rule.Matches("x10^9/L", 0.5m).Should().BeTrue();
        rule.Matches("x10^9/L", 5.0m).Should().BeFalse();
    }

    [Fact]
    public void Matches_WhenInactive_ShouldReturnFalse()
    {
        var rule = CriticalAlertRule.Create(
            "CBC",
            "Complete Blood Count",
            null,
            null,
            10.0m,
            "system",
            "System");

        rule.Deactivate();

        rule.Matches(null, 12.0m).Should().BeFalse();
    }

    [Fact]
    public void Create_WithInvalidThresholdRange_ShouldThrow()
    {
        var act = () => CriticalAlertRule.Create(
            "CBC",
            "Complete Blood Count",
            null,
            10.0m,
            1.0m,
            "system",
            "System");

        act.Should().Throw<DomainException>()
            .WithMessage("Low critical value cannot exceed high critical value.");
    }

    [Fact]
    public void CriticalAlertStatus_ShouldExposeExpectedValues()
    {
        CriticalAlertStatus.GetAll().Should().Contain(CriticalAlertStatus.Open);
        CriticalAlertStatus.GetAll().Should().Contain(CriticalAlertStatus.Acknowledged);
        CriticalAlertStatus.GetAll().Should().Contain(CriticalAlertStatus.Resolved);
    }

    [Fact]
    public void CriticalAlertTriggerType_ShouldExposeExpectedValues()
    {
        CriticalAlertTriggerType.GetAll().Should().Contain(CriticalAlertTriggerType.CriticalFlag);
        CriticalAlertTriggerType.GetAll().Should().Contain(CriticalAlertTriggerType.Threshold);
        CriticalAlertTriggerType.GetAll().Should().Contain(CriticalAlertTriggerType.Both);
    }
}
