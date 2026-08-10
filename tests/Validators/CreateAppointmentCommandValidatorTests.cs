using FluentValidation.TestHelper;
using His.Hope.AppointmentService.Application.UseCases.Appointments.Commands;

namespace His.Hope.Validators;

public class CreateAppointmentCommandValidatorTests
{
    private readonly CreateAppointmentCommandValidator _validator = new();

    private CreateAppointmentCommand ValidCommand => new(
        PatientId: Guid.NewGuid(),
        ProviderId: Guid.NewGuid(),
        ScheduledDate: DateTime.Today.AddDays(1),
        StartTime: new TimeSpan(9, 0, 0),
        DurationMinutes: 30,
        TypeCode: "CHECKUP",
        Reason: "Annual checkup",
        Location: "Room 101");

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
    public void PastScheduledDate_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { ScheduledDate = DateTime.Today.AddDays(-1) })
            .ShouldHaveValidationErrorFor(c => c.ScheduledDate);

    [Fact]
    public void DefaultStartTime_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { StartTime = default })
            .ShouldHaveValidationErrorFor(c => c.StartTime);

    [Fact]
    public void ZeroDuration_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { DurationMinutes = 0 })
            .ShouldHaveValidationErrorFor(c => c.DurationMinutes);

    [Fact]
    public void NegativeDuration_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { DurationMinutes = -5 })
            .ShouldHaveValidationErrorFor(c => c.DurationMinutes);

    [Fact]
    public void EmptyTypeCode_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { TypeCode = "" })
            .ShouldHaveValidationErrorFor(c => c.TypeCode);

    [Fact]
    public void InvalidTypeCode_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { TypeCode = "INVALID" })
            .ShouldHaveValidationErrorFor(c => c.TypeCode);

    [Fact]
    public void EmptyLocation_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Location = "" })
            .ShouldHaveValidationErrorFor(c => c.Location);

    [Fact]
    public void ValidTypeCodes_ShouldNotHaveError()
    {
        var validTypes = new[] { "CHECKUP", "CONSULT", "FOLLOWUP", "EMERG", "PROCED", "VACCINE", "LAB", "TELE" };
        foreach (var type in validTypes)
        {
            _validator.TestValidate(ValidCommand with { TypeCode = type })
                .ShouldNotHaveValidationErrorFor(c => c.TypeCode);
        }
    }

    [Fact]
    public void TodayScheduledDate_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { ScheduledDate = DateTime.Today })
            .ShouldNotHaveValidationErrorFor(c => c.ScheduledDate);
}
