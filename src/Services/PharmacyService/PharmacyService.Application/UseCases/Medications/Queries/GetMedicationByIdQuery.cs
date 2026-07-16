using His.Hope.PharmacyService.Application.DTOs;
using MediatR;

namespace His.Hope.PharmacyService.Application.UseCases.Medications.Queries;

public record GetMedicationByIdQuery(Guid Id) : IRequest<MedicationDto?>;
