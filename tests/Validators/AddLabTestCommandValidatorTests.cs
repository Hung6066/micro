using FluentValidation.TestHelper;
using His.Hope.LabService.Application.UseCases.LabOrders.Commands;

namespace His.Hope.Validators;

public class AddLabTestCommandValidatorTests
{
    private readonly AddLabTestCommandValidator _validator = new();

    private AddLabTestCommand ValidCommand => new(
        OrderId: Guid.NewGuid(),
        TestCode: "CBC",
        TestName: "Complete Blood Count",
        SpecimenType: "Blood");

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
    public void EmptyTestCode_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { TestCode = "" })
            .ShouldHaveValidationErrorFor(c => c.TestCode);

    [Fact]
    public void TestCodeOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { TestCode = new string('C', 21) })
            .ShouldHaveValidationErrorFor(c => c.TestCode);

    [Fact]
    public void EmptyTestName_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { TestName = "" })
            .ShouldHaveValidationErrorFor(c => c.TestName);

    [Fact]
    public void TestNameOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { TestName = new string('T', 201) })
            .ShouldHaveValidationErrorFor(c => c.TestName);

    [Fact]
    public void SpecimenTypeOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { SpecimenType = new string('S', 101) })
            .ShouldHaveValidationErrorFor(c => c.SpecimenType);

    [Fact]
    public void NullSpecimenType_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { SpecimenType = null })
            .ShouldNotHaveValidationErrorFor(c => c.SpecimenType);

    [Fact]
    public void TestCodeBorderline_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { TestCode = new string('C', 20) })
            .ShouldNotHaveValidationErrorFor(c => c.TestCode);
}
