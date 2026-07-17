using FluentAssertions;
using FluentValidation.TestHelper;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Validators;

namespace His.Hope.IdentityService.Application.Tests;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator;

    public LoginRequestValidatorTests()
    {
        _validator = new LoginRequestValidator();
    }

    [Fact]
    public void Validate_WithValidRequest_ShouldNotHaveErrors()
    {
        // Arrange
        var request = new LoginRequest(Username: "johndoe", Password: "Str0ng!Pass");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyUsername_ShouldHaveError()
    {
        // Arrange
        var request = new LoginRequest(Username: "", Password: "Str0ng!Pass");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Username)
            .WithErrorMessage("Username is required.");
    }

    [Fact]
    public void Validate_EmptyPassword_ShouldHaveError()
    {
        // Arrange
        var request = new LoginRequest(Username: "johndoe", Password: "");

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
        var request = new LoginRequest(Username: "johndoe", Password: "Ab1!");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Password)
            .WithErrorMessage("Password must be at least 6 characters.");
    }

    [Fact]
    public void Validate_TooLongUsername_ShouldHaveError()
    {
        // Arrange
        var request = new LoginRequest(
            Username: new string('a', 101),
            Password: "Str0ng!Pass");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Username)
            .WithErrorMessage("Username must not exceed 100 characters.");
    }

    [Fact]
    public void Validate_TooLongPassword_ShouldHaveError()
    {
        // Arrange
        var request = new LoginRequest(
            Username: "johndoe",
            Password: new string('A', 201));

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Password)
            .WithErrorMessage("Password must not exceed 200 characters.");
    }

    [Fact]
    public void Validate_BothEmpty_ShouldHaveTwoErrors()
    {
        // Arrange
        var request = new LoginRequest(Username: "", Password: "");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.Errors.Should().HaveCount(3);
        result.ShouldHaveValidationErrorFor(r => r.Username);
        result.ShouldHaveValidationErrorFor(r => r.Password);
    }

    [Fact]
    public void Validate_WhitespaceUsername_ShouldHaveError()
    {
        // Arrange
        var request = new LoginRequest(Username: "   ", Password: "Str0ng!Pass");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Username)
            .WithErrorMessage("Username is required.");
    }

    [Fact]
    public void Validate_WhitespacePassword_ShouldHaveError()
    {
        // Arrange
        var request = new LoginRequest(Username: "johndoe", Password: "   ");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Password)
            .WithErrorMessage("Password is required.");
    }
}
