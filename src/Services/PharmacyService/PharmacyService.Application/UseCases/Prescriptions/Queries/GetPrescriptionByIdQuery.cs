using His.Hope.PharmacyService.Application.DTOs;
using MediatR;

namespace His.Hope.PharmacyService.Application.UseCases.Prescriptions.Queries;

public record GetPrescriptionByIdQuery(Guid Id) : IRequest<PrescriptionDto?>;
