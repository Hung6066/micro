using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.Repositories;
using His.Hope.AppointmentService.Domain.ValueObjects;
using His.Hope.AppointmentService.Application.Common.Exceptions;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.AppointmentService.Application.UseCases.Appointments.Commands;

public class CheckInAppointmentCommandHandler : IRequestHandler<CheckInAppointmentCommand, Unit>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CheckInAppointmentCommandHandler(IAppointmentRepository appointmentRepository)
    {
        _appointmentRepository = appointmentRepository;
        _unitOfWork = appointmentRepository.UnitOfWork;
    }

    public async Task<Unit> Handle(CheckInAppointmentCommand request,
        CancellationToken cancellationToken)
    {
        var appointmentId = AppointmentId.From(request.Id);
        var appointment = await _appointmentRepository.GetByIdAsync(appointmentId, cancellationToken);

        if (appointment is null)
            throw new NotFoundException(nameof(Appointment), request.Id);

        appointment.CheckIn();

        await _appointmentRepository.UpdateAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
