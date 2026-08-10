using FluentValidation;

namespace His.Hope.PharmacyService.Application.UseCases.Prescriptions.Commands;

public class CreatePrescriptionCommandValidator : AbstractValidator<CreatePrescriptionCommand>
{
    public CreatePrescriptionCommandValidator()
    {
        RuleFor(v => v.PatientId)
            .NotEmpty().WithMessage("Patient ID is required.");

        RuleFor(v => v.ProviderId)
            .NotEmpty().WithMessage("Provider ID is required.");

        RuleFor(v => v.MedicationName)
            .NotEmpty().WithMessage("Medication name is required.")
            .MaximumLength(200).WithMessage("Medication name must not exceed 200 characters.");

        RuleFor(v => v.Strength)
            .NotEmpty().WithMessage("Strength is required.")
            .MaximumLength(50).WithMessage("Strength must not exceed 50 characters.");

        RuleFor(v => v.DosageForm)
            .NotEmpty().WithMessage("Dosage form is required.")
            .MaximumLength(50).WithMessage("Dosage form must not exceed 50 characters.");

        RuleFor(v => v.DosageInstructions)
            .NotEmpty().WithMessage("Dosage instructions are required.")
            .MaximumLength(500).WithMessage("Dosage instructions must not exceed 500 characters.");

        RuleFor(v => v.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than zero.");

        RuleFor(v => v.Refills)
            .GreaterThanOrEqualTo(0).WithMessage("Refills must be zero or greater.");

        RuleFor(v => v.Notes)
            .MaximumLength(1000).When(v => !string.IsNullOrEmpty(v.Notes));

        RuleFor(v => v.ExpiryDate)
            .GreaterThan(DateTime.UtcNow).When(v => v.ExpiryDate.HasValue)
            .WithMessage("Expiry date must be in the future.");
    }
}
