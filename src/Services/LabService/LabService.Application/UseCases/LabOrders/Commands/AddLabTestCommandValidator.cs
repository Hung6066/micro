using FluentValidation;

namespace His.Hope.LabService.Application.UseCases.LabOrders.Commands;

public class AddLabTestCommandValidator : AbstractValidator<AddLabTestCommand>
{
    public AddLabTestCommandValidator()
    {
        RuleFor(v => v.OrderId)
            .NotEmpty().WithMessage("Order ID is required.");

        RuleFor(v => v.TestCode)
            .NotEmpty().WithMessage("Test code is required.")
            .MaximumLength(20).WithMessage("Test code must not exceed 20 characters.");

        RuleFor(v => v.TestName)
            .NotEmpty().WithMessage("Test name is required.")
            .MaximumLength(200).WithMessage("Test name must not exceed 200 characters.");

        RuleFor(v => v.SpecimenType)
            .MaximumLength(100).When(v => !string.IsNullOrEmpty(v.SpecimenType));
    }
}
