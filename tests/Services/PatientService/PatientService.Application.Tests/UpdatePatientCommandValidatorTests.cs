using FluentAssertions;
using FluentValidation.TestHelper;
using His.Hope.PatientService.Application.UseCases.Patients.Commands;

namespace His.Hope.PatientService.Application.Tests;

public class UpdatePatientCommandValidatorTests
{
    private readonly UpdatePatientCommandValidator _validator = new();

    private UpdatePatientCommand CreateValidCommand() => new(
        Id: Guid.NewGuid(),
        FirstName: "Jane",
        LastName: "Smith",
        MiddleName: null,
        DateOfBirth: new DateTime(1985, 5, 20),
        GenderCode: "F",
        Phone: "+9876543210",
        Email: "jane@example.com",
        Street: "456 Oak Ave",
        District: "Uptown",
        City: "Gotham",
        Province: "State",
        PostalCode: "67890",
        Country: "USA");

    [Fact]
    public void Validate_WithValidCommand_ShouldNotHaveErrors()
    {
        var result = _validator.TestValidate(CreateValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyFirstName_ShouldHaveError()
    {
        var command = CreateValidCommand() with { FirstName = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.FirstName);
    }

    [Fact]
    public void Validate_WithEmptyLastName_ShouldHaveError()
    {
        var command = CreateValidCommand() with { LastName = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.LastName);
    }

    [Fact]
    public void Validate_WithEmptyPhone_ShouldHaveError()
    {
        var command = CreateValidCommand() with { Phone = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.Phone);
    }

    [Fact]
    public void Validate_WithInvalidGenderCode_ShouldHaveError()
    {
        var command = CreateValidCommand() with { GenderCode = "X" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.GenderCode);
    }

    [Fact]
    public void Validate_WithNullGenderCode_ShouldNotHaveError()
    {
        var command = CreateValidCommand() with { GenderCode = null };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(c => c.GenderCode);
    }

    [Fact]
    public void Validate_WithFutureDateOfBirth_ShouldHaveError()
    {
        var command = CreateValidCommand() with { DateOfBirth = DateTime.Today.AddDays(1) };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.DateOfBirth);
    }

    [Fact]
    public void Validate_WithEmptyStreet_ShouldHaveError()
    {
        var command = CreateValidCommand() with { Street = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.Street);
    }

    [Fact]
    public void Validate_WithEmptyCity_ShouldHaveError()
    {
        var command = CreateValidCommand() with { City = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.City);
    }

    [Fact]
    public void Validate_WithEmptyCountry_ShouldHaveError()
    {
        var command = CreateValidCommand() with { Country = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.Country);
    }
}
