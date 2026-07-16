using His.Hope.PharmacyService.Application.DTOs;
using MediatR;

namespace His.Hope.PharmacyService.Application.UseCases.Prescriptions.Commands;

public record CreatePrescriptionCommand(
    Guid PatientId,
    Guid ProviderId,
    Guid? MedicationId,
    string MedicationName,
    string Strength,
    string DosageForm,
    string DosageInstructions,
    string? Route,
    int Quantity,
    int Refills,
    string? Notes,
    DateTime? ExpiryDate) : IRequest<PrescriptionDto>;
