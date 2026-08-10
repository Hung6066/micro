using FluentAssertions;
using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.Exceptions;

namespace His.Hope.AppointmentService.Domain.Tests;

public class AppointmentBusinessRulesTests
{
    [Fact]
    public void AppointmentDateMustBeInFuture_WithYesterday_ShouldBeBroken()
    {
        var rule = new AppointmentDateMustBeInFuture(DateTime.Today.AddDays(-1));
        rule.IsBroken().Should().BeTrue();
        rule.Message.Should().Be("Appointment date must be today or in the future.");
    }

    [Fact]
    public void AppointmentDateMustBeInFuture_WithToday_ShouldNotBeBroken()
    {
        var rule = new AppointmentDateMustBeInFuture(DateTime.Today);
        rule.IsBroken().Should().BeFalse();
    }

    [Fact]
    public void AppointmentDateMustBeInFuture_WithTomorrow_ShouldNotBeBroken()
    {
        var rule = new AppointmentDateMustBeInFuture(DateTime.Today.AddDays(1));
        rule.IsBroken().Should().BeFalse();
    }

    [Fact]
    public void AppointmentDateMustBeInFuture_WithNextMonth_ShouldNotBeBroken()
    {
        var rule = new AppointmentDateMustBeInFuture(DateTime.Today.AddMonths(1));
        rule.IsBroken().Should().BeFalse();
    }

    [Fact]
    public void AppointmentDurationMustBePositive_WithZero_ShouldBeBroken()
    {
        var rule = new AppointmentDurationMustBePositive(0);
        rule.IsBroken().Should().BeTrue();
        rule.Message.Should().Be("Appointment duration must be positive.");
    }

    [Fact]
    public void AppointmentDurationMustBePositive_WithNegative_ShouldBeBroken()
    {
        var rule = new AppointmentDurationMustBePositive(-1);
        rule.IsBroken().Should().BeTrue();
    }

    [Fact]
    public void AppointmentDurationMustBePositive_WithPositive_ShouldNotBeBroken()
    {
        var rule = new AppointmentDurationMustBePositive(15);
        rule.IsBroken().Should().BeFalse();
    }

    [Fact]
    public void AppointmentDurationMustBePositive_WithLargeValue_ShouldNotBeBroken()
    {
        var rule = new AppointmentDurationMustBePositive(480);
        rule.IsBroken().Should().BeFalse();
    }

    [Fact]
    public void AppointmentMustBeScheduledToday_WithYesterday_ShouldBeBroken()
    {
        var rule = new AppointmentMustBeScheduledToday(DateTime.Today.AddDays(-1));
        rule.IsBroken().Should().BeTrue();
        rule.Message.Should().Be("Can only check in appointments scheduled for today.");
    }

    [Fact]
    public void AppointmentMustBeScheduledToday_WithToday_ShouldNotBeBroken()
    {
        var rule = new AppointmentMustBeScheduledToday(DateTime.Today);
        rule.IsBroken().Should().BeFalse();
    }

    [Fact]
    public void AppointmentMustBeScheduledToday_WithTomorrow_ShouldBeBroken()
    {
        var rule = new AppointmentMustBeScheduledToday(DateTime.Today.AddDays(1));
        rule.IsBroken().Should().BeTrue();
    }

    [Fact]
    public void BusinessRules_ShouldImplementIBusinessRule()
    {
        var dateRule = new AppointmentDateMustBeInFuture(DateTime.Today);
        var durationRule = new AppointmentDurationMustBePositive(30);
        var todayRule = new AppointmentMustBeScheduledToday(DateTime.Today);

        dateRule.Should().BeAssignableTo<IBusinessRule>();
        durationRule.Should().BeAssignableTo<IBusinessRule>();
        todayRule.Should().BeAssignableTo<IBusinessRule>();
    }

    [Fact]
    public void Schedule_WithDateThatFailsBusinessRule_ShouldThrowDomainException()
    {
        var act = () => Appointment.Schedule(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today.AddDays(-1),
            new TimeSpan(9, 0, 0), 30,
            AppointmentType.Checkup, null, null);

        act.Should().Throw<DomainException>()
            .WithMessage("Appointment date must be today or in the future.");
    }

    [Fact]
    public void CheckIn_WithFutureDate_ShouldThrowDomainException()
    {
        var appointment = Appointment.Schedule(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today.AddDays(7),
            new TimeSpan(9, 0, 0), 30, AppointmentType.Checkup, null, null);

        var act = () => appointment.CheckIn();

        act.Should().Throw<DomainException>()
            .WithMessage("Can only check in appointments scheduled for today.");
    }

    [Fact]
    public void Reschedule_WithPastDate_ShouldThrowDomainException()
    {
        var appointment = Appointment.Schedule(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today.AddDays(7),
            new TimeSpan(9, 0, 0), 30, AppointmentType.Checkup, null, null);

        var act = () => appointment.Reschedule(DateTime.Today.AddDays(-1), new TimeSpan(10, 0, 0), 30);

        act.Should().Throw<DomainException>()
            .WithMessage("Appointment date must be today or in the future.");
    }

    [Fact]
    public void Reschedule_WithZeroDuration_ShouldThrowDomainException()
    {
        var appointment = Appointment.Schedule(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today.AddDays(7),
            new TimeSpan(9, 0, 0), 30, AppointmentType.Checkup, null, null);

        var act = () => appointment.Reschedule(DateTime.Today.AddDays(14), new TimeSpan(10, 0, 0), 0);

        act.Should().Throw<DomainException>()
            .WithMessage("Appointment duration must be positive.");
    }
}
