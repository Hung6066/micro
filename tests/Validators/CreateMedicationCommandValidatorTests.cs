using FluentValidation.TestHelper;
using His.Hope.PharmacyService.Application.UseCases.Medications.Commands;

namespace His.Hope.Validators;

public class CreateMedicationCommandValidatorTests
{
    private readonly CreateMedicationCommandValidator _validator = new();

    private CreateMedicationCommand ValidCommand => new(
        Name: "Amoxicillin",
        GenericName: "Amoxicillin",
        BrandName: "Amoxil",
        DosageForm: "Capsule",
        Strength: "500mg",
        Route: "Oral",
        Category: "Antibiotic",
        Manufacturer: "TestPharma",
        RequiresPrescription: true);

    [Fact]
    public void ValidCommand_ShouldNotHaveErrors()
    {
        _validator.TestValidate(ValidCommand).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyName_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Name = "" })
            .ShouldHaveValidationErrorFor(c => c.Name);

    [Fact]
    public void NameOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Name = new string('A', 201) })
            .ShouldHaveValidationErrorFor(c => c.Name);

    [Fact]
    public void EmptyDosageForm_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { DosageForm = "" })
            .ShouldHaveValidationErrorFor(c => c.DosageForm);

    [Fact]
    public void EmptyStrength_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Strength = "" })
            .ShouldHaveValidationErrorFor(c => c.Strength);

    [Fact]
    public void GenericNameOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { GenericName = new string('G', 201) })
            .ShouldHaveValidationErrorFor(c => c.GenericName);

    [Fact]
    public void BrandNameOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { BrandName = new string('B', 201) })
            .ShouldHaveValidationErrorFor(c => c.BrandName);

    [Fact]
    public void RouteOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Route = new string('R', 51) })
            .ShouldHaveValidationErrorFor(c => c.Route);

    [Fact]
    public void NullGenericName_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { GenericName = null })
            .ShouldNotHaveValidationErrorFor(c => c.GenericName);

    [Fact]
    public void NullBrandName_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { BrandName = null })
            .ShouldNotHaveValidationErrorFor(c => c.BrandName);

    [Fact]
    public void NullRoute_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { Route = null })
            .ShouldNotHaveValidationErrorFor(c => c.Route);

    [Fact]
    public void NameBorderlineLength_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { Name = new string('A', 200) })
            .ShouldNotHaveValidationErrorFor(c => c.Name);
}
