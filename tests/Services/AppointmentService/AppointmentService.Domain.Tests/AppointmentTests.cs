using FluentAssertions;
using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.Exceptions;

namespace His.Hope.AppointmentService.Domain.Tests;

public class AppointmentTests
{
    private static readonly Guid PatientId = Guid.NewGuid();
    private static readonly Guid ProviderId = Guid.NewGuid();
    private static readonly DateTime FutureDate = DateTime.Today.AddDays(7);
    private static readonly TimeSpan StartTime = new(9, 0, 0);
    private const int DurationMinutes = 30;

    [Fact]
    public void Schedule_WithValidParameters_ShouldCreateScheduledAppointment()
    {
        // Act
        var appointment = Appointment.Schedule(
            PatientId, ProviderId, FutureDate, StartTime, DurationMinutes,
            AppointmentType.Checkup, "Annual checkup", "Clinic A");

        // Assert
        appointment.Should().NotBeNull();
        appointment.Id.Should().NotBeNull();
        appointment.PatientId.Should().Be(PatientId);
        appointment.ProviderId.Should().Be(ProviderId);
        appointment.ScheduledDate.Should().Be(FutureDate);
        appointment.StartTime.Should().Be(StartTime);
        appointment.EndTime.Should().Be(StartTime.Add(TimeSpan.FromMinutes(DurationMinutes)));
        appointment.Status.Should().Be(AppointmentStatus.Scheduled);
        appointment.Type.Should().Be(AppointmentType.Checkup);
        appointment.Reason.Should().Be("Annual checkup");
        appointment.Location.Should().Be("Clinic A");
        appointment.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        appointment.UpdatedAt.Should().BeNull();
        appointment.CheckedInAt.Should().BeNull();
        appointment.CheckedOutAt.Should().BeNull();
        appointment.CancelledAt.Should().BeNull();
        appointment.CancellationReason.Should().BeNull();
    }

    [Fact]
    public void Schedule_WithTomorrowDate_ShouldCreateAppointment()
    {
        // Arrange
        var tomorrow = DateTime.Today.AddDays(1);

        // Act
        var appointment = Appointment.Schedule(
            PatientId, ProviderId, tomorrow, StartTime, DurationMinutes,
            AppointmentType.Consultation, null, null);

        // Assert
        appointment.ScheduledDate.Should().Be(tomorrow);
        appointment.Status.Should().Be(AppointmentStatus.Scheduled);
    }

    [Fact]
    public void Schedule_WithTodayDate_ShouldCreateAppointment()
    {
        // Arrange
        var today = DateTime.Today;

        // Act
        var appointment = Appointment.Schedule(
            PatientId, ProviderId, today, StartTime, DurationMinutes,
            AppointmentType.Consultation, null, null);

        // Assert
        appointment.ScheduledDate.Should().Be(today);
    }

    [Fact]
    public void Schedule_WithPastDate_ShouldThrowDomainException()
    {
        // Arrange
        var pastDate = DateTime.Today.AddDays(-1);

        // Act
        var act = () => Appointment.Schedule(
            PatientId, ProviderId, pastDate, StartTime, DurationMinutes,
            AppointmentType.Checkup, null, null);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("Appointment date must be today or in the future.");
    }

    [Fact]
    public void Schedule_WithZeroDuration_ShouldThrowDomainException()
    {
        // Act
        var act = () => Appointment.Schedule(
            PatientId, ProviderId, FutureDate, StartTime, 0,
            AppointmentType.Checkup, null, null);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("Appointment duration must be positive.");
    }

    [Fact]
    public void Schedule_WithNegativeDuration_ShouldThrowDomainException()
    {
        // Act
        var act = () => Appointment.Schedule(
            PatientId, ProviderId, FutureDate, StartTime, -15,
            AppointmentType.Checkup, null, null);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("Appointment duration must be positive.");
    }

    [Fact]
    public void Schedule_WithDifferentTypes_ShouldSetCorrectType()
    {
        // Arrange
        var types = new[]
        {
            (AppointmentType.Checkup, AppointmentType.Checkup),
            (AppointmentType.Consultation, AppointmentType.Consultation),
            (AppointmentType.FollowUp, AppointmentType.FollowUp),
            (AppointmentType.Emergency, AppointmentType.Emergency),
            (AppointmentType.Procedure, AppointmentType.Procedure),
            (AppointmentType.Vaccination, AppointmentType.Vaccination),
            (AppointmentType.LabWork, AppointmentType.LabWork),
            (AppointmentType.Telehealth, AppointmentType.Telehealth)
        };

        foreach (var (type, expected) in types)
        {
            // Act
            var appointment = Appointment.Schedule(
                PatientId, ProviderId, FutureDate, StartTime, DurationMinutes,
                type, null, null);

            // Assert
            appointment.Type.Should().Be(expected);
        }
    }

    [Fact]
    public void Reschedule_WithFutureDate_ShouldUpdateAndSetRescheduledStatus()
    {
        // Arrange
        var appointment = Appointment.Schedule(
            PatientId, ProviderId, FutureDate, StartTime, DurationMinutes,
            AppointmentType.Checkup, null, null);

        var newDate = FutureDate.AddDays(14);
        var newStartTime = new TimeSpan(14, 0, 0);

        // Act
        appointment.Reschedule(newDate, newStartTime, 45);

        // Assert
        appointment.ScheduledDate.Should().Be(newDate);
        appointment.StartTime.Should().Be(newStartTime);
        appointment.EndTime.Should().Be(newStartTime.Add(TimeSpan.FromMinutes(45)));
        appointment.Status.Should().Be(AppointmentStatus.Rescheduled);
        appointment.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Reschedule_WithPastDate_ShouldThrowDomainException()
    {
        // Arrange
        var appointment = Appointment.Schedule(
            PatientId, ProviderId, FutureDate, StartTime, DurationMinutes,
            AppointmentType.Checkup, null, null);

        // Act
        var act = () => appointment.Reschedule(DateTime.Today.AddDays(-1), StartTime, 30);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("Appointment date must be today or in the future.");
    }

    [Fact]
    public void Cancel_WithReason_ShouldSetCancelledStatus()
    {
        // Arrange
        var appointment = Appointment.Schedule(
            PatientId, ProviderId, FutureDate, StartTime, DurationMinutes,
            AppointmentType.Checkup, "Original reason", "Clinic A");

        // Act
        appointment.Cancel("Patient requested cancellation");

        // Assert
        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
        appointment.CancelledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        appointment.CancellationReason.Should().Be("Patient requested cancellation");
        appointment.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Cancel_WithoutReason_ShouldSetCancelledStatusWithNullReason()
    {
        // Arrange
        var appointment = Appointment.Schedule(
            PatientId, ProviderId, FutureDate, StartTime, DurationMinutes,
            AppointmentType.Checkup, null, null);

        // Act
        appointment.Cancel(null);

        // Assert
        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
        appointment.CancelledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        appointment.CancellationReason.Should().BeNull();
    }

    [Fact]
    public void CheckIn_WithTodaysAppointment_ShouldSetCheckedInStatus()
    {
        // Arrange
        var today = DateTime.Today;
        var appointment = Appointment.Schedule(
            PatientId, ProviderId, today, StartTime, DurationMinutes,
            AppointmentType.Checkup, null, null);

        // Act
        appointment.CheckIn();

        // Assert
        appointment.Status.Should().Be(AppointmentStatus.CheckedIn);
        appointment.CheckedInAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        appointment.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CheckIn_WithFutureAppointment_ShouldThrowDomainException()
    {
        // Arrange
        var appointment = Appointment.Schedule(
            PatientId, ProviderId, FutureDate, StartTime, DurationMinutes,
            AppointmentType.Checkup, null, null);

        // Act
        var act = () => appointment.CheckIn();

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("Can only check in appointments scheduled for today.");
    }

    [Fact]
    public void CheckIn_WithPastAppointment_ShouldThrowDomainException()
    {
        // Arrange - use a today appointment then move its date back via reflection
        var appointment = Appointment.Schedule(
            PatientId, ProviderId, DateTime.Today, StartTime, DurationMinutes,
            AppointmentType.Checkup, null, null);

        var field = typeof(Appointment).GetField("<ScheduledDate>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field?.SetValue(appointment, DateTime.Today.AddDays(-5));

        // Act
        var act = () => appointment.CheckIn();

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("Can only check in appointments scheduled for today.");
    }

    [Fact]
    public void CheckOut_ShouldSetCompletedStatus()
    {
        // Arrange
        var today = DateTime.Today;
        var appointment = Appointment.Schedule(
            PatientId, ProviderId, today, StartTime, DurationMinutes,
            AppointmentType.Checkup, null, null);
        appointment.CheckIn();

        // Act
        appointment.CheckOut();

        // Assert
        appointment.Status.Should().Be(AppointmentStatus.Completed);
        appointment.CheckedOutAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        appointment.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CheckOut_WithoutCheckIn_ShouldStillComplete()
    {
        // Arrange
        var appointment = Appointment.Schedule(
            PatientId, ProviderId, FutureDate, StartTime, DurationMinutes,
            AppointmentType.Checkup, null, null);

        // Act
        appointment.CheckOut();

        // Assert
        appointment.Status.Should().Be(AppointmentStatus.Completed);
        appointment.CheckedOutAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateNotes_ShouldSetNotesAndUpdateTimestamp()
    {
        // Arrange
        var appointment = Appointment.Schedule(
            PatientId, ProviderId, FutureDate, StartTime, DurationMinutes,
            AppointmentType.Checkup, null, null);

        // Act
        appointment.UpdateNotes("Patient prefers afternoon appointments");

        // Assert
        appointment.Notes.Should().Be("Patient prefers afternoon appointments");
        appointment.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateNotes_WithNull_ShouldClearNotes()
    {
        // Arrange
        var appointment = Appointment.Schedule(
            PatientId, ProviderId, FutureDate, StartTime, DurationMinutes,
            AppointmentType.Checkup, "Initial reason", null);
        appointment.UpdateNotes("Some notes");

        // Act
        appointment.UpdateNotes(null);

        // Assert
        appointment.Notes.Should().BeNull();
    }

    [Fact]
    public void Schedule_FullWorkflow_ShouldSucceed()
    {
        // Arrange & Act - Schedule
        var appointment = Appointment.Schedule(
            PatientId, ProviderId, FutureDate, StartTime, DurationMinutes,
            AppointmentType.Checkup, "Routine visit", "Main Clinic");

        appointment.Status.Should().Be(AppointmentStatus.Scheduled);

        // Act - Reschedule
        var newDate = FutureDate.AddDays(1);
        appointment.Reschedule(newDate, new TimeSpan(10, 0, 0), 60);

        appointment.Status.Should().Be(AppointmentStatus.Rescheduled);
        appointment.ScheduledDate.Should().Be(newDate);

        // Act - Cancel (verify from rescheduled state)
        appointment.Cancel("Schedule conflict");

        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
        appointment.CancellationReason.Should().Be("Schedule conflict");
    }

    [Fact]
    public void Schedule_CompleteWorkflow_ShouldSucceed()
    {
        var today = DateTime.Today;

        // Schedule
        var appointment = Appointment.Schedule(
            PatientId, ProviderId, today, StartTime, DurationMinutes,
            AppointmentType.Consultation, "Follow-up", "Room 101");
        appointment.Status.Should().Be(AppointmentStatus.Scheduled);

        // Check In
        appointment.CheckIn();
        appointment.Status.Should().Be(AppointmentStatus.CheckedIn);
        appointment.CheckedInAt.Should().NotBeNull();

        // Check Out
        appointment.CheckOut();
        appointment.Status.Should().Be(AppointmentStatus.Completed);
        appointment.CheckedOutAt.Should().NotBeNull();
    }

    [Fact]
    public void TwoAppointments_WithSameId_ShouldBeEqual()
    {
        // Arrange
        var id = AppointmentId.New();
        var appointment1 = Appointment.Schedule(
            PatientId, ProviderId, FutureDate, StartTime, DurationMinutes,
            AppointmentType.Checkup, null, null);

        var appointment2 = Appointment.Schedule(
            PatientId, ProviderId, FutureDate, StartTime, DurationMinutes,
            AppointmentType.Checkup, null, null);

        // Different appointments should have different IDs
        appointment1.Equals(appointment2).Should().BeFalse();
    }

    [Fact]
    public void AppointmentId_WithEmptyGuid_ShouldThrow()
    {
        // Act
        var act = () => new AppointmentId(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("value");
    }

    [Fact]
    public void AppointmentId_WithValidGuid_ShouldCreate()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = new AppointmentId(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void AppointmentId_New_ShouldGenerateNonEmptyGuid()
    {
        // Act
        var id = AppointmentId.New();

        // Assert
        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void AppointmentId_From_ShouldCreateFromGuid()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = AppointmentId.From(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void AppointmentStatus_AllValues_ShouldBeDefined()
    {
        AppointmentStatus.Scheduled.Should().NotBeNull();
        AppointmentStatus.CheckedIn.Should().NotBeNull();
        AppointmentStatus.InProgress.Should().NotBeNull();
        AppointmentStatus.Completed.Should().NotBeNull();
        AppointmentStatus.Cancelled.Should().NotBeNull();
        AppointmentStatus.Rescheduled.Should().NotBeNull();
        AppointmentStatus.NoShow.Should().NotBeNull();

        // Verify all are distinct
        var statuses = new[]
        {
            AppointmentStatus.Scheduled,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.InProgress,
            AppointmentStatus.Completed,
            AppointmentStatus.Cancelled,
            AppointmentStatus.Rescheduled,
            AppointmentStatus.NoShow
        };

        statuses.Distinct().Should().HaveCount(7);
    }

    [Fact]
    public void AppointmentType_AllValues_ShouldBeDefined()
    {
        AppointmentType.Checkup.Should().NotBeNull();
        AppointmentType.Consultation.Should().NotBeNull();
        AppointmentType.FollowUp.Should().NotBeNull();
        AppointmentType.Emergency.Should().NotBeNull();
        AppointmentType.Procedure.Should().NotBeNull();
        AppointmentType.Vaccination.Should().NotBeNull();
        AppointmentType.LabWork.Should().NotBeNull();
        AppointmentType.Telehealth.Should().NotBeNull();

        // Verify all are distinct
        var types = new[]
        {
            AppointmentType.Checkup,
            AppointmentType.Consultation,
            AppointmentType.FollowUp,
            AppointmentType.Emergency,
            AppointmentType.Procedure,
            AppointmentType.Vaccination,
            AppointmentType.LabWork,
            AppointmentType.Telehealth
        };

        types.Distinct().Should().HaveCount(8);
    }

    [Fact]
    public void Appointment_WithNullReasonAndLocation_ShouldCreate()
    {
        // Act
        var appointment = Appointment.Schedule(
            PatientId, ProviderId, FutureDate, StartTime, DurationMinutes,
            AppointmentType.Telehealth, null, null);

        // Assert
        appointment.Reason.Should().BeNull();
        appointment.Location.Should().BeNull();
        appointment.Type.Should().Be(AppointmentType.Telehealth);
    }

    [Fact]
    public void BusinessRules_ShouldBeCorrect()
    {
        // Test AppointmentDateMustBeInFuture
        var pastRule = new AppointmentDateMustBeInFuture(DateTime.Today.AddDays(-1));
        pastRule.IsBroken().Should().BeTrue();

        var todayRule = new AppointmentDateMustBeInFuture(DateTime.Today);
        todayRule.IsBroken().Should().BeFalse();

        var futureRule = new AppointmentDateMustBeInFuture(DateTime.Today.AddDays(1));
        futureRule.IsBroken().Should().BeFalse();

        // Test AppointmentDurationMustBePositive
        var zeroDuration = new AppointmentDurationMustBePositive(0);
        zeroDuration.IsBroken().Should().BeTrue();

        var negativeDuration = new AppointmentDurationMustBePositive(-1);
        negativeDuration.IsBroken().Should().BeTrue();

        var positiveDuration = new AppointmentDurationMustBePositive(30);
        positiveDuration.IsBroken().Should().BeFalse();

        // Test AppointmentMustBeScheduledToday
        var pastAppt = new AppointmentMustBeScheduledToday(DateTime.Today.AddDays(-1));
        pastAppt.IsBroken().Should().BeTrue();

        var todayAppt = new AppointmentMustBeScheduledToday(DateTime.Today);
        todayAppt.IsBroken().Should().BeFalse();

        var futureAppt = new AppointmentMustBeScheduledToday(DateTime.Today.AddDays(1));
        futureAppt.IsBroken().Should().BeTrue();
    }
}
