using FluentValidation;

namespace His.Hope.PatientService.Application.UseCases.Patients.Commands;

public class CreatePatientCommandValidator : AbstractValidator<CreatePatientCommand>
{
    public CreatePatientCommandValidator()
    {
        RuleFor(v => v.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(v => v.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        RuleFor(v => v.DateOfBirth)
            .NotEmpty().WithMessage("Date of birth is required.")
            .LessThan(DateTime.Today).WithMessage("Date of birth must be in the past.");

        RuleFor(v => v.GenderCode)
            .NotEmpty().WithMessage("Gender is required.")
            .Must(g => new[] { "M", "F", "O", "U" }.Contains(g))
            .WithMessage("Invalid gender code. Must be M, F, O, or U.");

        RuleFor(v => v.Phone)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^\+?[0-9\s\-\(\)]{7,20}$").WithMessage("Invalid phone number format.");

        RuleFor(v => v.Email)
            .EmailAddress().When(v => !string.IsNullOrEmpty(v.Email))
            .WithMessage("Invalid email format.");

        RuleFor(v => v.Street)
            .NotEmpty().WithMessage("Street address is required.");

        RuleFor(v => v.City)
            .NotEmpty().WithMessage("City is required.");

        RuleFor(v => v.Province)
            .NotEmpty().WithMessage("Province is required.");

        RuleFor(v => v.Country)
            .NotEmpty().WithMessage("Country is required.");
    }
}
