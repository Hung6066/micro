using FluentValidation;

namespace His.Hope.LabService.Application.UseCases.LabOrders.Commands;

public class CreateLabOrderCommandValidator : AbstractValidator<CreateLabOrderCommand>
{
    public CreateLabOrderCommandValidator()
    {
        RuleFor(v => v.PatientId)
            .NotEmpty().WithMessage("Patient ID is required.");

        RuleFor(v => v.ProviderId)
            .NotEmpty().WithMessage("Provider ID is required.");

        RuleFor(v => v.PriorityCode)
            .NotEmpty().WithMessage("Priority code is required.");

        RuleFor(v => v.Tests)
            .NotEmpty().WithMessage("At least one test is required.");

        RuleForEach(v => v.Tests).ChildRules(test =>
        {
            test.RuleFor(t => t.TestCode)
                .NotEmpty().WithMessage("Test code is required.")
                .MaximumLength(20).WithMessage("Test code must not exceed 20 characters.");

            test.RuleFor(t => t.TestName)
                .NotEmpty().WithMessage("Test name is required.")
                .MaximumLength(200).WithMessage("Test name must not exceed 200 characters.");

            test.RuleFor(t => t.SpecimenType)
                .MaximumLength(100).When(t => !string.IsNullOrEmpty(t.SpecimenType));
        });

        RuleFor(v => v.Notes)
            .MaximumLength(1000).When(v => !string.IsNullOrEmpty(v.Notes));
    }
}
