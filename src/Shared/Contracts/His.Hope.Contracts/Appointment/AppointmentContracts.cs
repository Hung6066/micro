namespace His.Hope.Contracts.Appointment;

public sealed record ScheduleAppointmentRequest(
    Guid PatientId,
    Guid ProviderId,
    DateTime ScheduledDate,
    TimeSpan StartTime,
    int DurationMinutes,
    string TypeCode,
    string? Reason,
    string? Location);

public sealed record CancelAppointmentRequest(string? Reason);
