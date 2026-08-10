using AutoMapper;
using His.Hope.AppointmentService.Domain.Repositories;
using His.Hope.AppointmentService.Application.DTOs;
using MediatR;

namespace His.Hope.AppointmentService.Application.UseCases.Appointments.Queries;

public class SearchAppointmentsQueryHandler : IRequestHandler<SearchAppointmentsQuery, PagedResult<AppointmentDto>>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IMapper _mapper;

    public SearchAppointmentsQueryHandler(IAppointmentRepository appointmentRepository, IMapper mapper)
    {
        _appointmentRepository = appointmentRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<AppointmentDto>> Handle(SearchAppointmentsQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _appointmentRepository.SearchAsync(
            request.SearchTerm, request.Page, request.PageSize, cancellationToken);

        var dtos = _mapper.Map<List<AppointmentDto>>(items);

        return new PagedResult<AppointmentDto>(dtos, totalCount, request.Page, request.PageSize);
    }
}
