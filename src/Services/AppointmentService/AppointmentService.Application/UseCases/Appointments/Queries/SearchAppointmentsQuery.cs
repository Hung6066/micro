using His.Hope.AppointmentService.Application.DTOs;
using MediatR;

namespace His.Hope.AppointmentService.Application.UseCases.Appointments.Queries;

public record SearchAppointmentsQuery(string SearchTerm, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<AppointmentDto>>;
