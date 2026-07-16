using MediatR;

namespace His.Hope.AppointmentService.Application.UseCases.Appointments.Commands;

public record CheckInAppointmentCommand(Guid Id) : IRequest<Unit>;
