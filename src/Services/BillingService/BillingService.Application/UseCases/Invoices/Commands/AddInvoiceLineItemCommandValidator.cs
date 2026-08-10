using FluentValidation;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Commands;

public class AddInvoiceLineItemCommandValidator : AbstractValidator<AddInvoiceLineItemCommand>
{
    public AddInvoiceLineItemCommandValidator()
    {
        RuleFor(v => v.InvoiceId)
            .NotEmpty().WithMessage("Invoice ID is required.");

        RuleFor(v => v.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");

        RuleFor(v => v.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than zero.");

        RuleFor(v => v.UnitPrice)
            .GreaterThan(0).WithMessage("Unit price must be greater than zero.");

        RuleFor(v => v.ItemTypeCode)
            .Must(code => string.IsNullOrEmpty(code) || code is "SERVICE" or "MEDICATION" or "SUPPLY" or "PROCEDURE" or "LAB" or "CONSULTATION")
            .WithMessage("Invalid item type code.");
    }
}
