using FluentValidation;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Commands;

public class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceCommandValidator()
    {
        RuleFor(v => v.PatientId)
            .NotEmpty().WithMessage("Patient ID is required.");

        RuleFor(v => v.InvoiceNumber)
            .NotEmpty().WithMessage("Invoice number is required.")
            .MaximumLength(50).WithMessage("Invoice number must not exceed 50 characters.");

        RuleFor(v => v.InvoiceDate)
            .NotEmpty().WithMessage("Invoice date is required.");

        RuleFor(v => v.LineItems)
            .NotEmpty().WithMessage("At least one line item is required.");

        RuleForEach(v => v.LineItems).ChildRules(item =>
        {
            item.RuleFor(i => i.Description)
                .NotEmpty().WithMessage("Line item description is required.")
                .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than zero.");

            item.RuleFor(i => i.UnitPrice)
                .GreaterThan(0).WithMessage("Unit price must be greater than zero.");

            item.RuleFor(i => i.ItemTypeCode)
                .Must(code => string.IsNullOrEmpty(code) || code is "SERVICE" or "MEDICATION" or "SUPPLY" or "PROCEDURE" or "LAB" or "CONSULTATION")
                .WithMessage("Invalid item type code.");
        });

        RuleFor(v => v.Notes)
            .MaximumLength(1000).When(v => !string.IsNullOrEmpty(v.Notes));

        RuleFor(v => v.DueDate)
            .GreaterThan(v => v.InvoiceDate).When(v => v.DueDate.HasValue)
            .WithMessage("Due date must be after invoice date.");
    }
}
