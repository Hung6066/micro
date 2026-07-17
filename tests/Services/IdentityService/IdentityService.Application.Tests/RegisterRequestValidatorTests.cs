using FluentAssertions;
using FluentValidation.TestHelper;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Validators;

namespace His.Hope.IdentityService.Application.Tests;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator;

    public RegisterRequestValidatorTests()
    {
        _validator = new RegisterRequestValidator();
    }

    [Fact]
    public void Validate_WithValidRequest_ShouldNotHaveErrors()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "johndoe",
            Email: "john.doe@example.com",
            Password: "Str0ng!Pass",
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            LicenseNumber: null,
            Specialty: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithAllOptionalFields_ShouldNotHaveErrors()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "dr_smith",
            Email: "dr.smith@hospital.org",
            Password: "C0mpl3x!Pass",
            FirstName: "Jane",
            LastName: "Smith",
            MiddleName: "Marie",
            LicenseNumber: "LIC-12345",
            Specialty: "Cardiology");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyUsername_ShouldHaveError()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "",
            Email: "john.doe@example.com",
            Password: "Str0ng!Pass",
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            LicenseNumber: null,
            Specialty: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Username)
            .WithErrorMessage("Username is required.");
    }

    [Fact]
    public void Validate_TooShortUsername_ShouldHaveError()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "ab",
            Email: "john.doe@example.com",
            Password: "Str0ng!Pass",
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            LicenseNumber: null,
            Specialty: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Username)
            .WithErrorMessage("Username must be at least 3 characters.");
    }

    [Fact]
    public void Validate_UsernameWithSpecialChars_ShouldHaveError()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "john@doe!",
            Email: "john.doe@example.com",
            Password: "Str0ng!Pass",
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            LicenseNumber: null,
            Specialty: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Username)
            .WithErrorMessage("Username can only contain letters, numbers, and underscores.");
    }

    [Fact]
    public void Validate_EmptyEmail_ShouldHaveError()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "johndoe",
            Email: "",
            Password: "Str0ng!Pass",
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            LicenseNumber: null,
            Specialty: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Email)
            .WithErrorMessage("Email is required.");
    }

    [Fact]
    public void Validate_InvalidEmailFormat_ShouldHaveError()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "johndoe",
            Email: "not-an-email",
            Password: "Str0ng!Pass",
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            LicenseNumber: null,
            Specialty: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Email)
            .WithErrorMessage("Invalid email format.");
    }

    [Fact]
    public void Validate_EmptyPassword_ShouldHaveError()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "johndoe",
            Email: "john.doe@example.com",
            Password: "",
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            LicenseNumber: null,
            Specialty: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Password)
            .WithErrorMessage("Password is required.");
    }

    [Fact]
    public void Validate_TooShortPassword_ShouldHaveError()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "johndoe",
            Email: "john.doe@example.com",
            Password: "Ab1!",
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            LicenseNumber: null,
            Specialty: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Password)
            .WithErrorMessage("Password must be at least 8 characters.");
    }

    [Fact]
    public void Validate_PasswordWithoutUppercase_ShouldHaveError()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "johndoe",
            Email: "john.doe@example.com",
            Password: "str0ng!pass",
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            LicenseNumber: null,
            Specialty: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Password)
            .WithErrorMessage("Password must contain at least one uppercase letter.");
    }

    [Fact]
    public void Validate_PasswordWithoutLowercase_ShouldHaveError()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "johndoe",
            Email: "john.doe@example.com",
            Password: "STR0NG!PASS",
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            LicenseNumber: null,
            Specialty: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Password)
            .WithErrorMessage("Password must contain at least one lowercase letter.");
    }

    [Fact]
    public void Validate_PasswordWithoutDigit_ShouldHaveError()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "johndoe",
            Email: "john.doe@example.com",
            Password: "Strong!Pass",
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            LicenseNumber: null,
            Specialty: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Password)
            .WithErrorMessage("Password must contain at least one digit.");
    }

    [Fact]
    public void Validate_PasswordWithoutSpecialChar_ShouldHaveError()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "johndoe",
            Email: "john.doe@example.com",
            Password: "Str0ngPass1",
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            LicenseNumber: null,
            Specialty: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Password)
            .WithErrorMessage("Password must contain at least one special character.");
    }

    [Fact]
    public void Validate_EmptyFirstName_ShouldHaveError()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "johndoe",
            Email: "john.doe@example.com",
            Password: "Str0ng!Pass",
            FirstName: "",
            LastName: "Doe",
            MiddleName: null,
            LicenseNumber: null,
            Specialty: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.FirstName)
            .WithErrorMessage("First name is required.");
    }

    [Fact]
    public void Validate_EmptyLastName_ShouldHaveError()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "johndoe",
            Email: "john.doe@example.com",
            Password: "Str0ng!Pass",
            FirstName: "John",
            LastName: "",
            MiddleName: null,
            LicenseNumber: null,
            Specialty: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.LastName)
            .WithErrorMessage("Last name is required.");
    }

    [Fact]
    public void Validate_TooLongLicenseNumber_ShouldHaveError()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "johndoe",
            Email: "john.doe@example.com",
            Password: "Str0ng!Pass",
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            LicenseNumber: new string('A', 51),
            Specialty: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.LicenseNumber)
            .WithErrorMessage("License number must not exceed 50 characters.");
    }

    [Fact]
    public void Validate_TooLongSpecialty_ShouldHaveError()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "johndoe",
            Email: "john.doe@example.com",
            Password: "Str0ng!Pass",
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            LicenseNumber: null,
            Specialty: new string('A', 201));

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Specialty)
            .WithErrorMessage("Specialty must not exceed 200 characters.");
    }

    [Fact]
    public void Validate_NoErrorsForBoundaryLicenseNumberLength()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "johndoe",
            Email: "john.doe@example.com",
            Password: "Str0ng!Pass",
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            LicenseNumber: new string('A', 50),
            Specialty: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
