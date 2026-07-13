using AutoMapper;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.PatientService.Domain.Repositories;
using His.Hope.PatientService.Application.DTOs;
using MediatR;

namespace His.Hope.PatientService.Application.UseCases.Patients.Queries;

public class SearchPatientsQueryHandler : IRequestHandler<SearchPatientsQuery, PagedResult<PatientDto>>
{
    private readonly IPatientRepository _patientRepository;
    private readonly IMapper _mapper;

    public SearchPatientsQueryHandler(IPatientRepository patientRepository, IMapper mapper)
    {
        _patientRepository = patientRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<PatientDto>> Handle(SearchPatientsQuery request,
        CancellationToken cancellationToken)
    {
        var patients = await _patientRepository.SearchAsync(request.SearchTerm, cancellationToken);

        var paged = patients
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var dtos = _mapper.Map<List<PatientDto>>(paged);

        return new PagedResult<PatientDto>(dtos, patients.Count, request.Page, request.PageSize);
    }
}
