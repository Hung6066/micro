using His.Hope.AppointmentService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.AppointmentService.Domain.Aggregates;

public class Appointment : AggregateRoot<AppointmentId>
{
    public Guid PatientId { get; private set; }
    public Guid ProviderId { get; private set; }
    public DateTime ScheduledDate { get; private set; }
    public TimeSpan StartTime { get; private set; }
    public TimeSpan EndTime { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public AppointmentType Type { get; private set; }
    public string? Reason { get; private set; }
    public string? Notes { get; private set; }
    public string? Location { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? CheckedInAt { get; private set; }
    public DateTime? CheckedOutAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    private Appointment(
        AppointmentId id,
        Guid patientId,
        Guid providerId,
        DateTime scheduledDate,
        TimeSpan startTime,
        TimeSpan endTime,
        AppointmentType type,
        string? reason,
        string? location)
        : base(id)
    {
        PatientId = patientId;
        ProviderId = providerId;
        ScheduledDate = scheduledDate;
        StartTime = startTime;
        EndTime = endTime;
        Type = type;
        Reason = reason;
        Location = location;
        Status = AppointmentStatus.Scheduled;
        CreatedAt = DateTime.UtcNow;
    }

    public static Appointment Schedule(
        Guid patientId,
        Guid providerId,
        DateTime scheduledDate,
        TimeSpan startTime,
        int durationMinutes,
        AppointmentType type,
        string? reason,
        string? location)
    {
        Guard.Against.BusinessRule(new AppointmentDateMustBeInFuture(scheduledDate));
        Guard.Against.BusinessRule(new AppointmentDurationMustBePositive(durationMinutes));

        var id = AppointmentId.New();
        var endTime = startTime.Add(TimeSpan.FromMinutes(durationMinutes));

        return new Appointment(id, patientId, providerId, scheduledDate, startTime, endTime, type, reason, location);
    }

    public void Reschedule(DateTime newDate, TimeSpan newStartTime, int durationMinutes)
    {
        Guard.Against.BusinessRule(new AppointmentDateMustBeInFuture(newDate));
        Guard.Against.BusinessRule(new AppointmentDurationMustBePositive(durationMinutes));

        ScheduledDate = newDate;
        StartTime = newStartTime;
        EndTime = newStartTime.Add(TimeSpan.FromMinutes(durationMinutes));
        Status = AppointmentStatus.Rescheduled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel(string? reason)
    {
        Status = AppointmentStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        CancellationReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void CheckIn()
    {
        Guard.Against.BusinessRule(new AppointmentMustBeScheduledToday(ScheduledDate));
        Status = AppointmentStatus.CheckedIn;
        CheckedInAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void CheckOut()
    {
        Status = AppointmentStatus.Completed;
        CheckedOutAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes;
        UpdatedAt = DateTime.UtcNow;
    }

    private Appointment() { }
}

public class AppointmentDateMustBeInFuture : IBusinessRule
{
    private readonly DateTime _date;
    public AppointmentDateMustBeInFuture(DateTime date) => _date = date;
    public bool IsBroken() => _date.Date < DateTime.Today;
    public string Message => "Appointment date must be today or in the future.";
}

public class AppointmentDurationMustBePositive : IBusinessRule
{
    private readonly int _duration;
    public AppointmentDurationMustBePositive(int duration) => _duration = duration;
    public bool IsBroken() => _duration <= 0;
    public string Message => "Appointment duration must be positive.";
}

public class AppointmentMustBeScheduledToday : IBusinessRule
{
    private readonly DateTime _date;
    public AppointmentMustBeScheduledToday(DateTime date) => _date = date;
    public bool IsBroken() => _date.Date != DateTime.Today;
    public string Message => "Can only check in appointments scheduled for today.";
}
