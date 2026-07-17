using FluentAssertions;
using FluentValidation.TestHelper;
using His.Hope.AppointmentService.Application.UseCases.Appointments.Commands;

namespace His.Hope.AppointmentService.Application.Tests;

public class CreateAppointmentCommandValidatorTests
{
    private readonly CreateAppointmentCommandValidator _validator = new();

    private CreateAppointmentCommand CreateValidCommand() => new(
        PatientId: Guid.NewGuid(),
        ProviderId: Guid.NewGuid(),
        ScheduledDate: DateTime.Today.AddDays(7),
        StartTime: new TimeSpan(9, 0, 0),
        DurationMinutes: 30,
        TypeCode: "CHECKUP",
        Reason: "Annual checkup",
        Location: "Clinic A");

    [Fact]
    public void Validate_WithValidCommand_ShouldNotHaveErrors()
    {
        var result = _validator.TestValidate(CreateValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyPatientId_ShouldHaveError()
    {
        var command = CreateValidCommand() with { PatientId = Guid.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.PatientId);
    }

    [Fact]
    public void Validate_WithEmptyProviderId_ShouldHaveError()
    {
        var command = CreateValidCommand() with { ProviderId = Guid.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.ProviderId);
    }

    [Fact]
    public void Validate_WithPastScheduledDate_ShouldHaveError()
    {
        var command = CreateValidCommand() with { ScheduledDate = DateTime.Today.AddDays(-1) };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.ScheduledDate);
    }

    [Fact]
    public void Validate_WithTodayDate_ShouldNotHaveError()
    {
        var command = CreateValidCommand() with { ScheduledDate = DateTime.Today };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(c => c.ScheduledDate);
    }

    [Fact]
    public void Validate_WithZeroDuration_ShouldHaveError()
    {
        var command = CreateValidCommand() with { DurationMinutes = 0 };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.DurationMinutes);
    }

    [Fact]
    public void Validate_WithNegativeDuration_ShouldHaveError()
    {
        var command = CreateValidCommand() with { DurationMinutes = -15 };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.DurationMinutes);
    }

    [Fact]
    public void Validate_WithEmptyTypeCode_ShouldHaveError()
    {
        var command = CreateValidCommand() with { TypeCode = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.TypeCode);
    }

    [Theory]
    [InlineData("INVALID")]
    [InlineData("XX")]
    [InlineData("123")]
    public void Validate_WithInvalidTypeCode_ShouldHaveError(string invalidCode)
    {
        var command = CreateValidCommand() with { TypeCode = invalidCode };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.TypeCode);
    }

    [Theory]
    [InlineData("CHECKUP")]
    [InlineData("CONSULT")]
    [InlineData("FOLLOWUP")]
    [InlineData("EMERG")]
    [InlineData("PROCED")]
    [InlineData("VACCINE")]
    [InlineData("LAB")]
    [InlineData("TELE")]
    public void Validate_WithValidTypeCodes_ShouldNotHaveError(string typeCode)
    {
        var command = CreateValidCommand() with { TypeCode = typeCode };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(c => c.TypeCode);
    }

    [Fact]
    public void Validate_WithEmptyLocation_ShouldHaveError()
    {
        var command = CreateValidCommand() with { Location = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.Location);
    }

    [Fact]
    public void Validate_WithDefaultStartTime_ShouldHaveError()
    {
        var command = CreateValidCommand() with { StartTime = default };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.StartTime);
    }
}
