using AutoMapper;
using His.Hope.AppointmentService.Application.DTOs;
using His.Hope.AppointmentService.Domain.Repositories;
using MediatR;

namespace His.Hope.AppointmentService.Application.UseCases.Appointments.Queries;

public record GetAppointmentsByPatientQuery(
    Guid PatientId,
    int Page = 1,
    int PageSize = 20,
    DateTime? FromDate = null,
    DateTime? ToDate = null) : IRequest<PagedResult<AppointmentDto>>;

public class GetAppointmentsByPatientQueryHandler
    : IRequestHandler<GetAppointmentsByPatientQuery, PagedResult<AppointmentDto>>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IMapper _mapper;

    public GetAppointmentsByPatientQueryHandler(
        IAppointmentRepository appointmentRepository,
        IMapper mapper)
    {
        _appointmentRepository = appointmentRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<AppointmentDto>> Handle(
        GetAppointmentsByPatientQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _appointmentRepository.GetByPatientIdAsync(
            request.PatientId,
            request.Page,
            request.PageSize,
            request.FromDate,
            request.ToDate,
            cancellationToken);

        var dtos = _mapper.Map<List<AppointmentDto>>(items);
        return new PagedResult<AppointmentDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}
