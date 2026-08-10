using FluentValidation;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Commands;

public class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(v => v.InvoiceId)
            .NotEmpty().WithMessage("Invoice ID is required.");

        RuleFor(v => v.PatientId)
            .NotEmpty().WithMessage("Patient ID is required.");

        RuleFor(v => v.Amount)
            .GreaterThan(0).WithMessage("Payment amount must be greater than zero.");

        RuleFor(v => v.PaymentDate)
            .NotEmpty().WithMessage("Payment date is required.");

        RuleFor(v => v.MethodCode)
            .NotEmpty().WithMessage("Payment method is required.")
            .Must(code => code is "CASH" or "CREDIT_CARD" or "DEBIT_CARD" or "INSURANCE" or "BANK_TRANSFER" or "CHEQUE")
            .WithMessage("Invalid payment method code.");

        RuleFor(v => v.ReferenceNumber)
            .MaximumLength(100).When(v => !string.IsNullOrEmpty(v.ReferenceNumber));

        RuleFor(v => v.Notes)
            .MaximumLength(500).When(v => !string.IsNullOrEmpty(v.Notes));
    }
}
