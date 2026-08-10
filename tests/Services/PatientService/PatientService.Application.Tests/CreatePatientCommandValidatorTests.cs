using FluentAssertions;
using FluentValidation.TestHelper;
using His.Hope.PatientService.Application.UseCases.Patients.Commands;

namespace His.Hope.PatientService.Application.Tests;

public class CreatePatientCommandValidatorTests
{
    private readonly CreatePatientCommandValidator _validator = new();

    private CreatePatientCommand CreateValidCommand() => new(
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
    public void Validate_WithValidCommand_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var command = CreateValidCommand();

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyFirstName_ShouldHaveError()
    {
        // Arrange
        var command = CreateValidCommand() with { FirstName = "" };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.FirstName)
            .WithErrorMessage("First name is required.");
    }

    [Fact]
    public void Validate_WithFirstNameExceedingMaxLength_ShouldHaveError()
    {
        // Arrange
        var command = CreateValidCommand() with { FirstName = new string('A', 101) };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.FirstName)
            .WithErrorMessage("First name must not exceed 100 characters.");
    }

    [Fact]
    public void Validate_WithEmptyLastName_ShouldHaveError()
    {
        // Arrange
        var command = CreateValidCommand() with { LastName = "" };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.LastName)
            .WithErrorMessage("Last name is required.");
    }

    [Fact]
    public void Validate_WithLastNameExceedingMaxLength_ShouldHaveError()
    {
        // Arrange
        var command = CreateValidCommand() with { LastName = new string('B', 101) };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.LastName)
            .WithErrorMessage("Last name must not exceed 100 characters.");
    }

    [Fact]
    public void Validate_WithDefaultDateOfBirth_ShouldHaveError()
    {
        // Arrange
        var command = CreateValidCommand() with { DateOfBirth = default };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.DateOfBirth)
            .WithErrorMessage("Date of birth is required.");
    }

    [Fact]
    public void Validate_WithFutureDateOfBirth_ShouldHaveError()
    {
        // Arrange
        var command = CreateValidCommand() with { DateOfBirth = DateTime.Today.AddDays(1) };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.DateOfBirth)
            .WithErrorMessage("Date of birth must be in the past.");
    }

    [Fact]
    public void Validate_WithTodayDateOfBirth_ShouldHaveError()
    {
        // Arrange
        var command = CreateValidCommand() with { DateOfBirth = DateTime.Today };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.DateOfBirth)
            .WithErrorMessage("Date of birth must be in the past.");
    }

    [Fact]
    public void Validate_WithEmptyGenderCode_ShouldHaveError()
    {
        // Arrange
        var command = CreateValidCommand() with { GenderCode = "" };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.GenderCode)
            .WithErrorMessage("Gender is required.");
    }

    [Theory]
    [InlineData("X")]
    [InlineData("INVALID")]
    [InlineData("123")]
    public void Validate_WithInvalidGenderCode_ShouldHaveError(string invalidCode)
    {
        // Arrange
        var command = CreateValidCommand() with { GenderCode = invalidCode };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.GenderCode)
            .WithErrorMessage("Invalid gender code. Must be M, F, O, or U.");
    }

    [Theory]
    [InlineData("M")]
    [InlineData("F")]
    [InlineData("O")]
    [InlineData("U")]
    public void Validate_WithValidGenderCodes_ShouldNotHaveError(string validCode)
    {
        // Arrange
        var command = CreateValidCommand() with { GenderCode = validCode };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(c => c.GenderCode);
    }

    [Fact]
    public void Validate_WithEmptyPhone_ShouldHaveError()
    {
        // Arrange
        var command = CreateValidCommand() with { Phone = "" };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Phone)
            .WithErrorMessage("Phone number is required.");
    }

    [Theory]
    [InlineData("12")]
    [InlineData("abc")]
    [InlineData("123456789012345678901")]
    public void Validate_WithInvalidPhoneFormat_ShouldHaveError(string invalidPhone)
    {
        // Arrange
        var command = CreateValidCommand() with { Phone = invalidPhone };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Phone)
            .WithErrorMessage("Invalid phone number format.");
    }

    [Fact]
    public void Validate_WithInvalidEmail_ShouldHaveError()
    {
        // Arrange
        var command = CreateValidCommand() with { Email = "not-an-email" };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Email)
            .WithErrorMessage("Invalid email format.");
    }

    [Fact]
    public void Validate_WithNullEmail_ShouldNotHaveError()
    {
        // Arrange
        var command = CreateValidCommand() with { Email = null };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void Validate_WithEmptyStreet_ShouldHaveError()
    {
        // Arrange
        var command = CreateValidCommand() with { Street = "" };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Street)
            .WithErrorMessage("Street address is required.");
    }

    [Fact]
    public void Validate_WithEmptyCity_ShouldHaveError()
    {
        // Arrange
        var command = CreateValidCommand() with { City = "" };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.City)
            .WithErrorMessage("City is required.");
    }

    [Fact]
    public void Validate_WithEmptyProvince_ShouldHaveError()
    {
        // Arrange
        var command = CreateValidCommand() with { Province = "" };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Province)
            .WithErrorMessage("Province is required.");
    }

    [Fact]
    public void Validate_WithEmptyCountry_ShouldHaveError()
    {
        // Arrange
        var command = CreateValidCommand() with { Country = "" };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Country)
            .WithErrorMessage("Country is required.");
    }

    [Fact]
    public void Validate_WithAllRequiredErrors_ShouldHaveMultipleErrors()
    {
        // Arrange
        var command = CreateValidCommand() with
        {
            FirstName = "",
            LastName = "",
            GenderCode = "",
            Phone = "",
            Street = "",
            City = "",
            Province = "",
            Country = ""
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.Errors.Should().NotBeEmpty();
        result.ShouldHaveValidationErrorFor(c => c.FirstName);
        result.ShouldHaveValidationErrorFor(c => c.LastName);
        result.ShouldHaveValidationErrorFor(c => c.GenderCode);
        result.ShouldHaveValidationErrorFor(c => c.Phone);
        result.ShouldHaveValidationErrorFor(c => c.Street);
        result.ShouldHaveValidationErrorFor(c => c.City);
        result.ShouldHaveValidationErrorFor(c => c.Province);
        result.ShouldHaveValidationErrorFor(c => c.Country);
    }

    [Fact]
    public void Validate_WithBorderlineFirstNameLength_ShouldNotHaveError()
    {
        // Arrange
        var command = CreateValidCommand() with { FirstName = new string('A', 100) };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(c => c.FirstName);
    }

    [Fact]
    public void Validate_WithBorderlineLastNameLength_ShouldNotHaveError()
    {
        // Arrange
        var command = CreateValidCommand() with { LastName = new string('B', 100) };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(c => c.LastName);
    }
}
