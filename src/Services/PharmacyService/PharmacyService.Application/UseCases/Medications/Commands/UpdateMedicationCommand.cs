using His.Hope.PharmacyService.Application.DTOs;
using MediatR;

namespace His.Hope.PharmacyService.Application.UseCases.Medications.Commands;

public record UpdateMedicationCommand(
    Guid Id,
    string Name,
    string? GenericName,
    string? BrandName,
    string DosageForm,
    string Strength,
    string? Route,
    string? Category,
    string? Manufacturer,
    bool RequiresPrescription) : IRequest<MedicationDto>;
