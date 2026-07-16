using His.Hope.AppointmentService.Application.DTOs;
using MediatR;

namespace His.Hope.AppointmentService.Application.UseCases.Appointments.Queries;

public record GetAppointmentByIdQuery(Guid Id) : IRequest<AppointmentDto?>;
