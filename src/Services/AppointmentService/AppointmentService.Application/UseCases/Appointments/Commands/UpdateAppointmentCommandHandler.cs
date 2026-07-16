using AutoMapper;
using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.Repositories;
using His.Hope.AppointmentService.Domain.ValueObjects;
using His.Hope.AppointmentService.Application.Common.Exceptions;
using His.Hope.AppointmentService.Application.DTOs;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.AppointmentService.Application.UseCases.Appointments.Commands;

public class UpdateAppointmentCommandHandler : IRequestHandler<UpdateAppointmentCommand, AppointmentDto>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAppointmentCommandHandler(
        IAppointmentRepository appointmentRepository,
        IMapper mapper)
    {
        _appointmentRepository = appointmentRepository;
        _mapper = mapper;
        _unitOfWork = appointmentRepository.UnitOfWork;
    }

    public async Task<AppointmentDto> Handle(UpdateAppointmentCommand request,
        CancellationToken cancellationToken)
    {
        var appointmentId = AppointmentId.From(request.Id);
        var appointment = await _appointmentRepository.GetByIdAsync(appointmentId, cancellationToken);

        if (appointment is null)
            throw new NotFoundException(nameof(Appointment), request.Id);

        appointment.Reschedule(request.ScheduledDate, request.StartTime, request.DurationMinutes);

        await _appointmentRepository.UpdateAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<AppointmentDto>(appointment);
    }
}
