using His.Hope.PatientService.Application.DTOs;
using MediatR;

namespace His.Hope.PatientService.Application.UseCases.Patients.Queries;

public record SearchPatientsQuery(string SearchTerm, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<PatientDto>>;
