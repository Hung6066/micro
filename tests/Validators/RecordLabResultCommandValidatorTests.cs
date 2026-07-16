using FluentValidation.TestHelper;
using His.Hope.LabService.Application.UseCases.LabOrders.Commands;

namespace His.Hope.Validators;

public class RecordLabResultCommandValidatorTests
{
    private readonly RecordLabResultCommandValidator _validator = new();

    private RecordLabResultCommand ValidCommand => new(
        OrderId: Guid.NewGuid(),
        TestId: Guid.NewGuid(),
        Value: "5.2",
        Unit: "mg/dL",
        ReferenceRange: "3.5 - 5.5",
        AbnormalFlagCode: "H",
        ResultStatusCode: "FINAL",
        PerformedBy: "Dr. Smith",
        Notes: "Patient was fasting");

    [Fact]
    public void ValidCommand_ShouldNotHaveErrors()
    {
        _validator.TestValidate(ValidCommand).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyOrderId_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { OrderId = Guid.Empty })
            .ShouldHaveValidationErrorFor(c => c.OrderId);

    [Fact]
    public void EmptyTestId_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { TestId = Guid.Empty })
            .ShouldHaveValidationErrorFor(c => c.TestId);

    [Fact]
    public void EmptyValue_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Value = "" })
            .ShouldHaveValidationErrorFor(c => c.Value);

    [Fact]
    public void ValueOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Value = new string('V', 501) })
            .ShouldHaveValidationErrorFor(c => c.Value);

    [Fact]
    public void EmptyResultStatusCode_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { ResultStatusCode = "" })
            .ShouldHaveValidationErrorFor(c => c.ResultStatusCode);

    [Fact]
    public void UnitOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Unit = new string('U', 51) })
            .ShouldHaveValidationErrorFor(c => c.Unit);

    [Fact]
    public void ReferenceRangeOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { ReferenceRange = new string('R', 101) })
            .ShouldHaveValidationErrorFor(c => c.ReferenceRange);

    [Fact]
    public void PerformedByOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { PerformedBy = new string('P', 201) })
            .ShouldHaveValidationErrorFor(c => c.PerformedBy);

    [Fact]
    public void NotesOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Notes = new string('N', 1001) })
            .ShouldHaveValidationErrorFor(c => c.Notes);

    [Fact]
    public void NullUnit_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { Unit = null })
            .ShouldNotHaveValidationErrorFor(c => c.Unit);

    [Fact]
    public void NullNotes_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { Notes = null })
            .ShouldNotHaveValidationErrorFor(c => c.Notes);
}
