using His.Hope.PharmacyService.Application.DTOs;
using MediatR;

namespace His.Hope.PharmacyService.Application.UseCases.Prescriptions.Queries;

public record GetPrescriptionsByPatientQuery(
    Guid PatientId, IReadOnlySet<string>? FacilityIds = null, bool CrossFacility = false)
    : IRequest<IReadOnlyList<PrescriptionDto>>;
