using His.Hope.PharmacyService.Application.DTOs;
using MediatR;

namespace His.Hope.PharmacyService.Application.UseCases.Prescriptions.Queries;

public record GetPrescriptionsByPatientQuery(Guid PatientId) : IRequest<IReadOnlyList<PrescriptionDto>>;
