namespace His.Hope.AppointmentService.Application.DTOs;

public record CreateAppointmentRequest(
    Guid PatientId,
    Guid ProviderId,
    DateTime ScheduledDate,
    TimeSpan StartTime,
    int DurationMinutes,
    string TypeCode,
    string? Reason,
    string? Location);

public record UpdateAppointmentRequest(
    Guid Id,
    DateTime ScheduledDate,
    TimeSpan StartTime,
    int DurationMinutes);
