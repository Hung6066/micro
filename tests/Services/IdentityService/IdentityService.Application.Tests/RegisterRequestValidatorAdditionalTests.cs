using FluentAssertions;
using FluentValidation.TestHelper;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Validators;

namespace His.Hope.IdentityService.Application.Tests;

public class RegisterRequestValidatorAdditionalTests
{
    private readonly RegisterRequestValidator _validator = new();

    private RegisterRequest CreateValidRequest() => new(
        Username: "johndoe",
        Email: "john.doe@example.com",
        Password: "Str0ng!Pass",
        FirstName: "John",
        LastName: "Doe",
        MiddleName: null,
        LicenseNumber: null,
        Specialty: null);

    [Fact]
    public void Validate_WithUsernameAtMinLength_ShouldNotHaveError()
    {
        var request = CreateValidRequest() with { Username = "abc" };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(r => r.Username);
    }

    [Fact]
    public void Validate_WithUsernameAtMaxLength_ShouldNotHaveError()
    {
        var request = CreateValidRequest() with { Username = new string('a', 100) };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(r => r.Username);
    }

    [Fact]
    public void Validate_WithUsernameExceedingMaxLength_ShouldHaveError()
    {
        var request = CreateValidRequest() with { Username = new string('a', 101) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(r => r.Username)
            .WithErrorMessage("Username must not exceed 100 characters.");
    }

    [Fact]
    public void Validate_WithEmailExceedingMaxLength_ShouldHaveError()
    {
        var request = CreateValidRequest() with { Email = new string('a', 195) + "@b.com" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(r => r.Email)
            .WithErrorMessage("Email must not exceed 200 characters.");
    }

    [Fact]
    public void Validate_WithEmailAtMaxLength_ShouldNotHaveError()
    {
        var localPart = new string('a', 188);
        var request = CreateValidRequest() with { Email = $"{localPart}@b.co" };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(r => r.Email);
    }

    [Fact]
    public void Validate_WithNullEmail_ShouldHaveError()
    {
        var request = CreateValidRequest() with { Email = null! };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(r => r.Email)
            .WithErrorMessage("Email is required.");
    }

    [Fact]
    public void Validate_WithPasswordExceedingMaxLength_ShouldHaveError()
    {
        var request = CreateValidRequest() with { Password = new string('A', 201) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(r => r.Password)
            .WithErrorMessage("Password must not exceed 200 characters.");
    }

    [Fact]
    public void Validate_WithNullFirstName_ShouldHaveError()
    {
        var request = CreateValidRequest() with { FirstName = null! };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(r => r.FirstName)
            .WithErrorMessage("First name is required.");
    }

    [Fact]
    public void Validate_WithNullLastName_ShouldHaveError()
    {
        var request = CreateValidRequest() with { LastName = null! };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(r => r.LastName)
            .WithErrorMessage("Last name is required.");
    }

    [Fact]
    public void Validate_WithFirstNameExceedingMaxLength_ShouldHaveError()
    {
        var request = CreateValidRequest() with { FirstName = new string('A', 101) };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(r => r.FirstName)
            .WithErrorMessage("First name must not exceed 100 characters.");
    }

    [Fact]
    public void Validate_WithWhitespaceFirstName_ShouldHaveError()
    {
        var request = CreateValidRequest() with { FirstName = "   " };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(r => r.FirstName);
    }

    [Fact]
    public void Validate_WithDeviceInfo_ShouldNotFail()
    {
        var request = CreateValidRequest() with { DeviceInfo = "Mozilla/5.0", IpAddress = null };
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithIpAddress_ShouldNotFail()
    {
        var request = CreateValidRequest() with { DeviceInfo = null, IpAddress = "10.0.0.1" };
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithWhitespaceMiddleName_ShouldNotHaveError()
    {
        var request = CreateValidRequest() with { MiddleName = "   " };
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithAllErrors_ShouldHaveManyErrors()
    {
        var request = CreateValidRequest() with
        {
            Username = "",
            Email = "",
            Password = "",
            FirstName = "",
            LastName = ""
        };

        var result = _validator.TestValidate(request);

        result.Errors.Should().HaveCountGreaterThanOrEqualTo(5);
    }
}
