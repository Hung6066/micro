using AutoMapper;
using His.Hope.AppointmentService.Domain.Repositories;
using His.Hope.AppointmentService.Domain.ValueObjects;
using His.Hope.AppointmentService.Application.DTOs;
using MediatR;

namespace His.Hope.AppointmentService.Application.UseCases.Appointments.Queries;

public class GetAppointmentByIdQueryHandler : IRequestHandler<GetAppointmentByIdQuery, AppointmentDto?>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IMapper _mapper;

    public GetAppointmentByIdQueryHandler(IAppointmentRepository appointmentRepository, IMapper mapper)
    {
        _appointmentRepository = appointmentRepository;
        _mapper = mapper;
    }

    public async Task<AppointmentDto?> Handle(GetAppointmentByIdQuery request,
        CancellationToken cancellationToken)
    {
        var appointmentId = AppointmentId.From(request.Id);
        var appointment = await _appointmentRepository.GetByIdAsync(appointmentId, cancellationToken);

        return appointment is null ? null : _mapper.Map<AppointmentDto>(appointment);
    }
}
