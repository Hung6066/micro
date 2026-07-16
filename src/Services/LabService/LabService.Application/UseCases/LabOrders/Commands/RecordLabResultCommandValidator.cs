using FluentValidation;

namespace His.Hope.LabService.Application.UseCases.LabOrders.Commands;

public class RecordLabResultCommandValidator : AbstractValidator<RecordLabResultCommand>
{
    public RecordLabResultCommandValidator()
    {
        RuleFor(v => v.OrderId)
            .NotEmpty().WithMessage("Order ID is required.");

        RuleFor(v => v.TestId)
            .NotEmpty().WithMessage("Test ID is required.");

        RuleFor(v => v.Value)
            .NotEmpty().WithMessage("Result value is required.")
            .MaximumLength(500).WithMessage("Result value must not exceed 500 characters.");

        RuleFor(v => v.ResultStatusCode)
            .NotEmpty().WithMessage("Result status code is required.");

        RuleFor(v => v.Unit)
            .MaximumLength(50).When(v => !string.IsNullOrEmpty(v.Unit));

        RuleFor(v => v.ReferenceRange)
            .MaximumLength(100).When(v => !string.IsNullOrEmpty(v.ReferenceRange));

        RuleFor(v => v.PerformedBy)
            .MaximumLength(200).When(v => !string.IsNullOrEmpty(v.PerformedBy));

        RuleFor(v => v.Notes)
            .MaximumLength(1000).When(v => !string.IsNullOrEmpty(v.Notes));
    }
}
