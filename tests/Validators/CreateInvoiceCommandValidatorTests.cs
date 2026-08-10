using FluentValidation.TestHelper;
using His.Hope.BillingService.Application.UseCases.Invoices.Commands;

namespace His.Hope.Validators;

public class CreateInvoiceCommandValidatorTests
{
    private readonly CreateInvoiceCommandValidator _validator = new();

    private CreateInvoiceCommand ValidCommand => new(
        PatientId: Guid.NewGuid(),
        EncounterId: Guid.NewGuid(),
        InvoiceDate: DateTime.UtcNow,
        DueDate: DateTime.UtcNow.AddDays(30),
        InvoiceNumber: "INV-2024-001",
        Notes: "Regular consultation",
        LineItems: new List<LineItemInput>
        {
            new("Consultation fee", 1, 150.00m, "CONS001", "SERVICE"),
            new("Lab work", 1, 75.00m, "LAB001", "LAB")
        });

    [Fact]
    public void ValidCommand_ShouldNotHaveErrors()
    {
        _validator.TestValidate(ValidCommand).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyPatientId_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { PatientId = Guid.Empty })
            .ShouldHaveValidationErrorFor(c => c.PatientId);

    [Fact]
    public void EmptyInvoiceNumber_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { InvoiceNumber = "" })
            .ShouldHaveValidationErrorFor(c => c.InvoiceNumber);

    [Fact]
    public void InvoiceNumberOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { InvoiceNumber = new string('I', 51) })
            .ShouldHaveValidationErrorFor(c => c.InvoiceNumber);

    [Fact]
    public void DefaultInvoiceDate_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { InvoiceDate = default })
            .ShouldHaveValidationErrorFor(c => c.InvoiceDate);

    [Fact]
    public void EmptyLineItems_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { LineItems = new List<LineItemInput>() })
            .ShouldHaveValidationErrorFor(c => c.LineItems);

    [Fact]
    public void LineItemWithEmptyDescription_ShouldHaveError()
    {
        var cmd = ValidCommand with
        {
            LineItems = new List<LineItemInput>
            {
                new("", 1, 100m, null, "SERVICE")
            }
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor("LineItems[0].Description");
    }

    [Fact]
    public void LineItemWithZeroQuantity_ShouldHaveError()
    {
        var cmd = ValidCommand with
        {
            LineItems = new List<LineItemInput>
            {
                new("Item", 0, 100m, null, "SERVICE")
            }
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor("LineItems[0].Quantity");
    }

    [Fact]
    public void LineItemWithZeroUnitPrice_ShouldHaveError()
    {
        var cmd = ValidCommand with
        {
            LineItems = new List<LineItemInput>
            {
                new("Item", 1, 0m, null, "SERVICE")
            }
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor("LineItems[0].UnitPrice");
    }

    [Fact]
    public void LineItemWithInvalidItemTypeCode_ShouldHaveError()
    {
        var cmd = ValidCommand with
        {
            LineItems = new List<LineItemInput>
            {
                new("Item", 1, 100m, null, "INVALID_TYPE")
            }
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor("LineItems[0].ItemTypeCode");
    }

    [Fact]
    public void LineItemWithNullItemTypeCode_ShouldNotHaveError()
    {
        var cmd = ValidCommand with
        {
            LineItems = new List<LineItemInput>
            {
                new("Item", 1, 100m, null, null)
            }
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor("LineItems[0].ItemTypeCode");
    }

    [Fact]
    public void NotesOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Notes = new string('N', 1001) })
            .ShouldHaveValidationErrorFor(c => c.Notes);

    [Fact]
    public void DueDateBeforeInvoiceDate_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { DueDate = ValidCommand.InvoiceDate.AddDays(-1) })
            .ShouldHaveValidationErrorFor(c => c.DueDate);

    [Fact]
    public void DueDateAfterInvoiceDate_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { DueDate = ValidCommand.InvoiceDate.AddDays(1) })
            .ShouldNotHaveValidationErrorFor(c => c.DueDate);

    [Fact]
    public void NullDueDate_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { DueDate = null })
            .ShouldNotHaveValidationErrorFor(c => c.DueDate);

    [Fact]
    public void ValidItemTypeCodes_ShouldNotHaveError()
    {
        var validTypes = new[] { "SERVICE", "MEDICATION", "SUPPLY", "PROCEDURE", "LAB", "CONSULTATION" };
        foreach (var type in validTypes)
        {
            var cmd = ValidCommand with
            {
                LineItems = new List<LineItemInput>
                {
                    new("Item", 1, 100m, null, type)
                }
            };
            _validator.TestValidate(cmd).ShouldNotHaveValidationErrorFor("LineItems[0].ItemTypeCode");
        }
    }
}
