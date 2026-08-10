using MediatR;

namespace His.Hope.AppointmentService.Application.UseCases.Appointments.Commands;

public record CheckOutAppointmentCommand(Guid Id) : IRequest<Unit>;
