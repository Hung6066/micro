using His.Hope.AppointmentService.Application.DTOs;
using MediatR;

namespace His.Hope.AppointmentService.Application.UseCases.Appointments.Commands;

public record UpdateAppointmentCommand(
    Guid Id,
    DateTime ScheduledDate,
    TimeSpan StartTime,
    int DurationMinutes) : IRequest<AppointmentDto>;
