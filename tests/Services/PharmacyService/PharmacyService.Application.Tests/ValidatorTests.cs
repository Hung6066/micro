using FluentValidation.TestHelper;
using His.Hope.PharmacyService.Application.UseCases.Medications.Commands;
using His.Hope.PharmacyService.Application.UseCases.Prescriptions.Commands;

namespace His.Hope.PharmacyService.Application.Tests;

public class ValidatorTests
{
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
        public void StrengthOverMax_ShouldHaveError() =>
            _validator.TestValidate(ValidCommand with { Strength = new string('S', 51) })
                .ShouldHaveValidationErrorFor(c => c.Strength);

        [Fact]
        public void EmptyDosageForm_ShouldHaveError() =>
            _validator.TestValidate(ValidCommand with { DosageForm = "" })
                .ShouldHaveValidationErrorFor(c => c.DosageForm);

        [Fact]
        public void DosageFormOverMax_ShouldHaveError() =>
            _validator.TestValidate(ValidCommand with { DosageForm = new string('D', 51) })
                .ShouldHaveValidationErrorFor(c => c.DosageForm);

        [Fact]
        public void EmptyDosageInstructions_ShouldHaveError() =>
            _validator.TestValidate(ValidCommand with { DosageInstructions = "" })
                .ShouldHaveValidationErrorFor(c => c.DosageInstructions);

        [Fact]
        public void DosageInstructionsOverMax_ShouldHaveError() =>
            _validator.TestValidate(ValidCommand with { DosageInstructions = new string('X', 501) })
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
        public void MedicationNameBorderlineLength_ShouldNotHaveError() =>
            _validator.TestValidate(ValidCommand with { MedicationName = new string('A', 200) })
                .ShouldNotHaveValidationErrorFor(c => c.MedicationName);

        [Fact]
        public void NullNotes_ShouldNotHaveError() =>
            _validator.TestValidate(ValidCommand with { Notes = null })
                .ShouldNotHaveValidationErrorFor(c => c.Notes);

        [Fact]
        public void NullExpiryDate_ShouldNotHaveError() =>
            _validator.TestValidate(ValidCommand with { ExpiryDate = null })
                .ShouldNotHaveValidationErrorFor(c => c.ExpiryDate);
    }

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
        public void DosageFormOverMax_ShouldHaveError() =>
            _validator.TestValidate(ValidCommand with { DosageForm = new string('D', 51) })
                .ShouldHaveValidationErrorFor(c => c.DosageForm);

        [Fact]
        public void EmptyStrength_ShouldHaveError() =>
            _validator.TestValidate(ValidCommand with { Strength = "" })
                .ShouldHaveValidationErrorFor(c => c.Strength);

        [Fact]
        public void StrengthOverMax_ShouldHaveError() =>
            _validator.TestValidate(ValidCommand with { Strength = new string('S', 51) })
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
}
