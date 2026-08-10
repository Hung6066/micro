using FluentValidation.TestHelper;
using His.Hope.BillingService.Application.UseCases.Invoices.Commands;

namespace His.Hope.Validators;

public class AddInvoiceLineItemCommandValidatorTests
{
    private readonly AddInvoiceLineItemCommandValidator _validator = new();

    private AddInvoiceLineItemCommand ValidCommand => new(
        InvoiceId: Guid.NewGuid(),
        Description: "Consultation fee",
        Quantity: 1,
        UnitPrice: 150.00m,
        ItemCode: "CONS001",
        ItemTypeCode: "SERVICE");

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
    public void EmptyDescription_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Description = "" })
            .ShouldHaveValidationErrorFor(c => c.Description);

    [Fact]
    public void DescriptionOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Description = new string('D', 501) })
            .ShouldHaveValidationErrorFor(c => c.Description);

    [Fact]
    public void ZeroQuantity_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Quantity = 0 })
            .ShouldHaveValidationErrorFor(c => c.Quantity);

    [Fact]
    public void NegativeQuantity_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Quantity = -1 })
            .ShouldHaveValidationErrorFor(c => c.Quantity);

    [Fact]
    public void ZeroUnitPrice_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { UnitPrice = 0 })
            .ShouldHaveValidationErrorFor(c => c.UnitPrice);

    [Fact]
    public void NegativeUnitPrice_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { UnitPrice = -10 })
            .ShouldHaveValidationErrorFor(c => c.UnitPrice);

    [Fact]
    public void InvalidItemTypeCode_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { ItemTypeCode = "INVALID" })
            .ShouldHaveValidationErrorFor(c => c.ItemTypeCode);

    [Fact]
    public void NullItemTypeCode_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { ItemTypeCode = null })
            .ShouldNotHaveValidationErrorFor(c => c.ItemTypeCode);

    [Fact]
    public void ValidItemTypeCodes_ShouldNotHaveError()
    {
        var validTypes = new[] { "SERVICE", "MEDICATION", "SUPPLY", "PROCEDURE", "LAB", "CONSULTATION" };
        foreach (var type in validTypes)
        {
            _validator.TestValidate(ValidCommand with { ItemTypeCode = type })
                .ShouldNotHaveValidationErrorFor(c => c.ItemTypeCode);
        }
    }

    [Fact]
    public void DescriptionBorderline_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { Description = new string('D', 500) })
            .ShouldNotHaveValidationErrorFor(c => c.Description);
}
