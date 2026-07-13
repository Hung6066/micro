using His.Hope.PatientService.Application.DTOs;
using MediatR;

namespace His.Hope.PatientService.Application.UseCases.Patients.Queries;

public record GetPatientByIdQuery(Guid Id) : IRequest<PatientDto?>;
