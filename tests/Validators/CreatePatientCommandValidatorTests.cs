using FluentValidation.TestHelper;
using His.Hope.PatientService.Application.UseCases.Patients.Commands;

namespace His.Hope.Validators;

public class CreatePatientCommandValidatorTests
{
    private readonly CreatePatientCommandValidator _validator = new();

    private CreatePatientCommand ValidCommand => new(
        FirstName: "John",
        LastName: "Doe",
        MiddleName: null,
        DateOfBirth: new DateTime(1990, 1, 15),
        GenderCode: "M",
        Phone: "+1234567890",
        Email: "john@example.com",
        Street: "123 Main St",
        District: "Downtown",
        City: "Metropolis",
        Province: "State",
        PostalCode: "12345",
        Country: "USA",
        InsuranceId: null,
        NationalId: null);

    [Fact]
    public void ValidCommand_ShouldNotHaveErrors()
    {
        _validator.TestValidate(ValidCommand).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyFirstName_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { FirstName = "" })
            .ShouldHaveValidationErrorFor(c => c.FirstName);

    [Fact]
    public void FirstNameOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { FirstName = new string('A', 101) })
            .ShouldHaveValidationErrorFor(c => c.FirstName);

    [Fact]
    public void EmptyLastName_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { LastName = "" })
            .ShouldHaveValidationErrorFor(c => c.LastName);

    [Fact]
    public void FutureDateOfBirth_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { DateOfBirth = DateTime.Today.AddDays(1) })
            .ShouldHaveValidationErrorFor(c => c.DateOfBirth);

    [Fact]
    public void InvalidGenderCode_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { GenderCode = "X" })
            .ShouldHaveValidationErrorFor(c => c.GenderCode);

    [Fact]
    public void EmptyPhone_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Phone = "" })
            .ShouldHaveValidationErrorFor(c => c.Phone);

    [Fact]
    public void InvalidPhone_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Phone = "abc" })
            .ShouldHaveValidationErrorFor(c => c.Phone);

    [Fact]
    public void InvalidEmail_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Email = "invalid" })
            .ShouldHaveValidationErrorFor(c => c.Email);

    [Fact]
    public void EmptyStreet_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Street = "" })
            .ShouldHaveValidationErrorFor(c => c.Street);
}
