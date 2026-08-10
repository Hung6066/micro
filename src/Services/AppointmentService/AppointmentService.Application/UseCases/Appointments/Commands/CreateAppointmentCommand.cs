using His.Hope.AppointmentService.Application.DTOs;
using MediatR;

namespace His.Hope.AppointmentService.Application.UseCases.Appointments.Commands;

public record CreateAppointmentCommand(
    Guid PatientId,
    Guid ProviderId,
    DateTime ScheduledDate,
    TimeSpan StartTime,
    int DurationMinutes,
    string TypeCode,
    string? Reason,
    string? Location) : IRequest<AppointmentDto>;
