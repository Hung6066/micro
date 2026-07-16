using FluentValidation;

namespace His.Hope.PharmacyService.Application.UseCases.Medications.Commands;

public class CreateMedicationCommandValidator : AbstractValidator<CreateMedicationCommand>
{
    public CreateMedicationCommandValidator()
    {
        RuleFor(v => v.Name)
            .NotEmpty().WithMessage("Medication name is required.")
            .MaximumLength(200).WithMessage("Medication name must not exceed 200 characters.");

        RuleFor(v => v.DosageForm)
            .NotEmpty().WithMessage("Dosage form is required.")
            .MaximumLength(50).WithMessage("Dosage form must not exceed 50 characters.");

        RuleFor(v => v.Strength)
            .NotEmpty().WithMessage("Strength is required.")
            .MaximumLength(50).WithMessage("Strength must not exceed 50 characters.");

        RuleFor(v => v.GenericName)
            .MaximumLength(200).When(v => !string.IsNullOrEmpty(v.GenericName));

        RuleFor(v => v.BrandName)
            .MaximumLength(200).When(v => !string.IsNullOrEmpty(v.BrandName));

        RuleFor(v => v.Route)
            .MaximumLength(50).When(v => !string.IsNullOrEmpty(v.Route));
    }
}
