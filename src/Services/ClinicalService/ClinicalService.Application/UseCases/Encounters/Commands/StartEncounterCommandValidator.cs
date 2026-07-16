using FluentValidation;

namespace His.Hope.ClinicalService.Application.UseCases.Encounters.Commands;

public class StartEncounterCommandValidator : AbstractValidator<StartEncounterCommand>
{
    private static readonly string[] ValidEncounterTypes = ["OP", "IP", "ER", "TH", "FU", "AW"];

    public StartEncounterCommandValidator()
    {
        RuleFor(v => v.PatientId)
            .NotEmpty().WithMessage("Patient ID is required.");

        RuleFor(v => v.ProviderId)
            .NotEmpty().WithMessage("Provider ID is required.");

        RuleFor(v => v.EncounterTypeCode)
            .NotEmpty().WithMessage("Encounter type is required.")
            .Must(BeValidEncounterType)
            .WithMessage("Invalid encounter type. Must be one of: OP, IP, ER, TH, FU, AW.");

        RuleFor(v => v.EncounterDate)
            .Must(d => d.HasValue && d.Value <= DateTime.UtcNow)
            .When(v => v.EncounterDate.HasValue)
            .WithMessage("Encounter date must not be in the future.");
    }

    private static bool BeValidEncounterType(string code) =>
        ValidEncounterTypes.Contains(code);
}
