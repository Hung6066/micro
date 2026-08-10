using AutoMapper;
using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.Repositories;
using His.Hope.AppointmentService.Domain.ValueObjects;
using His.Hope.AppointmentService.Application.DTOs;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.AppointmentService.Application.UseCases.Appointments.Commands;

public class CreateAppointmentCommandHandler : IRequestHandler<CreateAppointmentCommand, AppointmentDto>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAppointmentCommandHandler(
        IAppointmentRepository appointmentRepository,
        IMapper mapper)
    {
        _appointmentRepository = appointmentRepository;
        _mapper = mapper;
        _unitOfWork = appointmentRepository.UnitOfWork;
    }

    public async Task<AppointmentDto> Handle(CreateAppointmentCommand request,
        CancellationToken cancellationToken)
    {
        var type = AppointmentType.FromCode(request.TypeCode);

        var appointment = Appointment.Schedule(
            request.PatientId,
            request.ProviderId,
            request.ScheduledDate,
            request.StartTime,
            request.DurationMinutes,
            type,
            request.Reason,
            request.Location);

        await _appointmentRepository.AddAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<AppointmentDto>(appointment);
    }
}
