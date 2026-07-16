using FluentValidation.TestHelper;
using His.Hope.BillingService.Application.UseCases.Invoices.Commands;

namespace His.Hope.Validators;

public class RecordPaymentCommandValidatorTests
{
    private readonly RecordPaymentCommandValidator _validator = new();

    private RecordPaymentCommand ValidCommand => new(
        InvoiceId: Guid.NewGuid(),
        PatientId: Guid.NewGuid(),
        Amount: 250.00m,
        PaymentDate: DateTime.UtcNow,
        MethodCode: "CREDIT_CARD",
        ReferenceNumber: "TXN-123456",
        Notes: "Paid in full");

    [Fact]
    public void ValidCommand_ShouldNotHaveErrors()
    {
        _validator.TestValidate(ValidCommand).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyInvoiceId_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { InvoiceId = Guid.Empty })
            .ShouldHaveValidationErrorFor(c => c.InvoiceId);

    [Fact]
    public void EmptyPatientId_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { PatientId = Guid.Empty })
            .ShouldHaveValidationErrorFor(c => c.PatientId);

    [Fact]
    public void ZeroAmount_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Amount = 0 })
            .ShouldHaveValidationErrorFor(c => c.Amount);

    [Fact]
    public void NegativeAmount_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Amount = -50 })
            .ShouldHaveValidationErrorFor(c => c.Amount);

    [Fact]
    public void DefaultPaymentDate_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { PaymentDate = default })
            .ShouldHaveValidationErrorFor(c => c.PaymentDate);

    [Fact]
    public void EmptyMethodCode_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { MethodCode = "" })
            .ShouldHaveValidationErrorFor(c => c.MethodCode);

    [Theory]
    [InlineData("CASH")]
    [InlineData("CREDIT_CARD")]
    [InlineData("DEBIT_CARD")]
    [InlineData("INSURANCE")]
    [InlineData("BANK_TRANSFER")]
    [InlineData("CHEQUE")]
    public void ValidMethodCodes_ShouldNotHaveError(string methodCode) =>
        _validator.TestValidate(ValidCommand with { MethodCode = methodCode })
            .ShouldNotHaveValidationErrorFor(c => c.MethodCode);

    [Fact]
    public void InvalidMethodCode_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { MethodCode = "BITCOIN" })
            .ShouldHaveValidationErrorFor(c => c.MethodCode);

    [Fact]
    public void ReferenceNumberOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { ReferenceNumber = new string('R', 101) })
            .ShouldHaveValidationErrorFor(c => c.ReferenceNumber);

    [Fact]
    public void NotesOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Notes = new string('N', 501) })
            .ShouldHaveValidationErrorFor(c => c.Notes);

    [Fact]
    public void NullReferenceNumber_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { ReferenceNumber = null })
            .ShouldNotHaveValidationErrorFor(c => c.ReferenceNumber);

    [Fact]
    public void NullNotes_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { Notes = null })
            .ShouldNotHaveValidationErrorFor(c => c.Notes);
}
