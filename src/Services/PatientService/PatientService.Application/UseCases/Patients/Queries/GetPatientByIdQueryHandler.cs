using AutoMapper;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.PatientService.Domain.Repositories;
using His.Hope.PatientService.Application.DTOs;
using MediatR;

namespace His.Hope.PatientService.Application.UseCases.Patients.Queries;

public class GetPatientByIdQueryHandler : IRequestHandler<GetPatientByIdQuery, PatientDto?>
{
    private readonly IPatientRepository _patientRepository;
    private readonly IMapper _mapper;

    public GetPatientByIdQueryHandler(IPatientRepository patientRepository, IMapper mapper)
    {
        _patientRepository = patientRepository;
        _mapper = mapper;
    }

    public async Task<PatientDto?> Handle(GetPatientByIdQuery request,
        CancellationToken cancellationToken)
    {
        var patientId = PatientId.From(request.Id);
        var patient = await _patientRepository.GetByIdAsync(patientId, cancellationToken);

        return patient is null ? null : _mapper.Map<PatientDto>(patient);
    }
}
