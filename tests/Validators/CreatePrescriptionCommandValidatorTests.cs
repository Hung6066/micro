using FluentValidation.TestHelper;
using His.Hope.PharmacyService.Application.UseCases.Prescriptions.Commands;

namespace His.Hope.Validators;

public class CreatePrescriptionCommandValidatorTests
{
    private readonly CreatePrescriptionCommandValidator _validator = new();

    private CreatePrescriptionCommand ValidCommand => new(
        PatientId: Guid.NewGuid(),
        ProviderId: Guid.NewGuid(),
        MedicationId: Guid.NewGuid(),
        MedicationName: "Amoxicillin",
        Strength: "500mg",
        DosageForm: "Capsule",
        DosageInstructions: "Take one capsule three times daily",
        Route: "Oral",
        Quantity: 30,
        Refills: 2,
        Notes: "Take with food",
        ExpiryDate: DateTime.UtcNow.AddMonths(6));

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
    public void EmptyMedicationName_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { MedicationName = "" })
            .ShouldHaveValidationErrorFor(c => c.MedicationName);

    [Fact]
    public void MedicationNameOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { MedicationName = new string('A', 201) })
            .ShouldHaveValidationErrorFor(c => c.MedicationName);

    [Fact]
    public void EmptyStrength_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Strength = "" })
            .ShouldHaveValidationErrorFor(c => c.Strength);

    [Fact]
    public void EmptyDosageForm_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { DosageForm = "" })
            .ShouldHaveValidationErrorFor(c => c.DosageForm);

    [Fact]
    public void EmptyDosageInstructions_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { DosageInstructions = "" })
            .ShouldHaveValidationErrorFor(c => c.DosageInstructions);

    [Fact]
    public void ZeroQuantity_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Quantity = 0 })
            .ShouldHaveValidationErrorFor(c => c.Quantity);

    [Fact]
    public void NegativeQuantity_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Quantity = -1 })
            .ShouldHaveValidationErrorFor(c => c.Quantity);

    [Fact]
    public void NegativeRefills_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Refills = -1 })
            .ShouldHaveValidationErrorFor(c => c.Refills);

    [Fact]
    public void ZeroRefills_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { Refills = 0 })
            .ShouldNotHaveValidationErrorFor(c => c.Refills);

    [Fact]
    public void PastExpiryDate_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { ExpiryDate = DateTime.UtcNow.AddDays(-1) })
            .ShouldHaveValidationErrorFor(c => c.ExpiryDate);

    [Fact]
    public void NotesOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Notes = new string('N', 1001) })
            .ShouldHaveValidationErrorFor(c => c.Notes);

    [Fact]
    public void MedicationsNameBorderlineLength_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { MedicationName = new string('A', 200) })
            .ShouldNotHaveValidationErrorFor(c => c.MedicationName);
}
