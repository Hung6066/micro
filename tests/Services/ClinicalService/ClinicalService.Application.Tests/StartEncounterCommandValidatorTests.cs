using FluentAssertions;
using FluentValidation.TestHelper;
using His.Hope.ClinicalService.Application.UseCases.Encounters.Commands;

namespace His.Hope.ClinicalService.Application.Tests;

public class StartEncounterCommandValidatorTests
{
    private readonly StartEncounterCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var command = new StartEncounterCommand(
            PatientId: Guid.NewGuid(),
            ProviderId: Guid.NewGuid(),
            AppointmentId: null,
            EncounterTypeCode: "OP",
            EncounterDate: null,
            ChiefComplaint: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithAllFields_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var command = new StartEncounterCommand(
            PatientId: Guid.NewGuid(),
            ProviderId: Guid.NewGuid(),
            AppointmentId: Guid.NewGuid(),
            EncounterTypeCode: "ER",
            EncounterDate: DateTime.UtcNow.AddHours(-1),
            ChiefComplaint: "Chest pain");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyPatientId_ShouldHaveError()
    {
        // Arrange
        var command = new StartEncounterCommand(
            PatientId: Guid.Empty,
            ProviderId: Guid.NewGuid(),
            AppointmentId: null,
            EncounterTypeCode: "OP",
            EncounterDate: null,
            ChiefComplaint: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.PatientId)
            .WithErrorMessage("Patient ID is required.");
    }

    [Fact]
    public void Validate_WithEmptyProviderId_ShouldHaveError()
    {
        // Arrange
        var command = new StartEncounterCommand(
            PatientId: Guid.NewGuid(),
            ProviderId: Guid.Empty,
            AppointmentId: null,
            EncounterTypeCode: "OP",
            EncounterDate: null,
            ChiefComplaint: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.ProviderId)
            .WithErrorMessage("Provider ID is required.");
    }

    [Fact]
    public void Validate_WithEmptyEncounterTypeCode_ShouldHaveError()
    {
        // Arrange
        var command = new StartEncounterCommand(
            PatientId: Guid.NewGuid(),
            ProviderId: Guid.NewGuid(),
            AppointmentId: null,
            EncounterTypeCode: "",
            EncounterDate: null,
            ChiefComplaint: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.EncounterTypeCode)
            .WithErrorMessage("Encounter type is required.");
    }

    [Theory]
    [InlineData("INVALID")]
    [InlineData("XX")]
    [InlineData("123")]
    [InlineData("op")]
    [InlineData("er")]
    public void Validate_WithInvalidEncounterTypeCode_ShouldHaveError(string invalidCode)
    {
        // Arrange
        var command = new StartEncounterCommand(
            PatientId: Guid.NewGuid(),
            ProviderId: Guid.NewGuid(),
            AppointmentId: null,
            EncounterTypeCode: invalidCode,
            EncounterDate: null,
            ChiefComplaint: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.EncounterTypeCode)
            .WithErrorMessage("Invalid encounter type. Must be one of: OP, IP, ER, TH, FU, AW.");
    }

    [Theory]
    [InlineData("OP")]
    [InlineData("IP")]
    [InlineData("ER")]
    [InlineData("TH")]
    [InlineData("FU")]
    [InlineData("AW")]
    public void Validate_WithValidEncounterTypeCodes_ShouldNotHaveError(string validCode)
    {
        // Arrange
        var command = new StartEncounterCommand(
            PatientId: Guid.NewGuid(),
            ProviderId: Guid.NewGuid(),
            AppointmentId: null,
            EncounterTypeCode: validCode,
            EncounterDate: null,
            ChiefComplaint: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(c => c.EncounterTypeCode);
    }

    [Fact]
    public void Validate_WithFutureEncounterDate_ShouldHaveError()
    {
        // Arrange
        var command = new StartEncounterCommand(
            PatientId: Guid.NewGuid(),
            ProviderId: Guid.NewGuid(),
            AppointmentId: null,
            EncounterTypeCode: "OP",
            EncounterDate: DateTime.UtcNow.AddDays(1),
            ChiefComplaint: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.EncounterDate)
            .WithErrorMessage("Encounter date must not be in the future.");
    }

    [Fact]
    public void Validate_WithPastEncounterDate_ShouldNotHaveError()
    {
        // Arrange
        var command = new StartEncounterCommand(
            PatientId: Guid.NewGuid(),
            ProviderId: Guid.NewGuid(),
            AppointmentId: null,
            EncounterTypeCode: "OP",
            EncounterDate: DateTime.UtcNow.AddDays(-1),
            ChiefComplaint: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(c => c.EncounterDate);
    }

    [Fact]
    public void Validate_WithAllErrors_ShouldHaveMultipleErrors()
    {
        // Arrange
        var command = new StartEncounterCommand(
            PatientId: Guid.Empty,
            ProviderId: Guid.Empty,
            AppointmentId: null,
            EncounterTypeCode: "",
            EncounterDate: DateTime.UtcNow.AddHours(2),
            ChiefComplaint: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        // Empty encounter type triggers both NotEmpty and Must validators
        result.Errors.Should().HaveCount(5);
        result.ShouldHaveValidationErrorFor(c => c.PatientId);
        result.ShouldHaveValidationErrorFor(c => c.ProviderId);
        result.ShouldHaveValidationErrorFor(c => c.EncounterTypeCode);
        result.ShouldHaveValidationErrorFor(c => c.EncounterDate);
    }

    [Fact]
    public void Validate_WithCurrentDate_ShouldNotHaveError()
    {
        // Arrange
        var command = new StartEncounterCommand(
            PatientId: Guid.NewGuid(),
            ProviderId: Guid.NewGuid(),
            AppointmentId: null,
            EncounterTypeCode: "OP",
            EncounterDate: DateTime.UtcNow,
            ChiefComplaint: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(c => c.EncounterDate);
    }
}
