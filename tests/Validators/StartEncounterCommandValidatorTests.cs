using FluentValidation.TestHelper;
using His.Hope.ClinicalService.Application.UseCases.Encounters.Commands;

namespace His.Hope.Validators;

public class StartEncounterCommandValidatorTests
{
    private readonly StartEncounterCommandValidator _validator = new();

    private StartEncounterCommand ValidCommand => new(
        PatientId: Guid.NewGuid(),
        ProviderId: Guid.NewGuid(),
        AppointmentId: null,
        EncounterTypeCode: "OP",
        EncounterDate: null,
        ChiefComplaint: null);

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
    public void EmptyEncounterType_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { EncounterTypeCode = "" })
            .ShouldHaveValidationErrorFor(c => c.EncounterTypeCode);

    [Theory]
    [InlineData("XX")]
    [InlineData("INVALID")]
    [InlineData("op")]
    public void InvalidEncounterType_ShouldHaveError(string typeCode) =>
        _validator.TestValidate(ValidCommand with { EncounterTypeCode = typeCode })
            .ShouldHaveValidationErrorFor(c => c.EncounterTypeCode);

    [Fact]
    public void FutureEncounterDate_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { EncounterDate = DateTime.UtcNow.AddDays(1) })
            .ShouldHaveValidationErrorFor(c => c.EncounterDate);
}
