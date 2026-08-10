using FluentValidation.TestHelper;
using His.Hope.LabService.Application.UseCases.LabOrders.Commands;

namespace His.Hope.Validators;

public class CreateLabOrderCommandValidatorTests
{
    private readonly CreateLabOrderCommandValidator _validator = new();

    private CreateLabOrderCommand ValidCommand => new(
        PatientId: Guid.NewGuid(),
        ProviderId: Guid.NewGuid(),
        EncounterId: Guid.NewGuid(),
        PriorityCode: "ROUTINE",
        Notes: "Fasting required",
        Tests: new List<TestItem>
        {
            new("CBC", "Complete Blood Count", "Blood"),
            new("BMP", "Basic Metabolic Panel", "Blood")
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
    public void EmptyProviderId_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { ProviderId = Guid.Empty })
            .ShouldHaveValidationErrorFor(c => c.ProviderId);

    [Fact]
    public void EmptyPriorityCode_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { PriorityCode = "" })
            .ShouldHaveValidationErrorFor(c => c.PriorityCode);

    [Fact]
    public void EmptyTestsList_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Tests = new List<TestItem>() })
            .ShouldHaveValidationErrorFor(c => c.Tests);

    [Fact]
    public void NullTests_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Tests = null! })
            .ShouldHaveValidationErrorFor(c => c.Tests);

    [Fact]
    public void TestWithEmptyCode_ShouldHaveError()
    {
        var cmd = ValidCommand with
        {
            Tests = new List<TestItem> { new("", "Test Name", "Blood") }
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor("Tests[0].TestCode");
    }

    [Fact]
    public void TestWithEmptyName_ShouldHaveError()
    {
        var cmd = ValidCommand with
        {
            Tests = new List<TestItem> { new("CBC", "", "Blood") }
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor("Tests[0].TestName");
    }

    [Fact]
    public void TestWithCodeOverMax_ShouldHaveError()
    {
        var cmd = ValidCommand with
        {
            Tests = new List<TestItem> { new(new string('C', 21), "Test", "Blood") }
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor("Tests[0].TestCode");
    }

    [Fact]
    public void NotesOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Notes = new string('N', 1001) })
            .ShouldHaveValidationErrorFor(c => c.Notes);

    [Fact]
    public void NullNotes_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { Notes = null })
            .ShouldNotHaveValidationErrorFor(c => c.Notes);
}
