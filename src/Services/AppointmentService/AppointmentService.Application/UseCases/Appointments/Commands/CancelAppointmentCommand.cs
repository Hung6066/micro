using MediatR;

namespace His.Hope.AppointmentService.Application.UseCases.Appointments.Commands;

public record CancelAppointmentCommand(Guid Id, string? Reason) : IRequest<Unit>;
