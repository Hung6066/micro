using FluentAssertions;
using FluentValidation.TestHelper;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Validators;

namespace His.Hope.IdentityService.Application.Tests;

public class LoginRequestValidatorAdditionalTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Validate_WithUsernameAtMaxLength_ShouldNotHaveError()
    {
        var request = new LoginRequest(new string('a', 100), "Str0ng!Pass");
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(r => r.Username);
    }

    [Fact]
    public void Validate_WithPasswordAtMinLength_ShouldNotHaveError()
    {
        var request = new LoginRequest("johndoe", "Ab1!ef");
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithPasswordAtMaxLength_ShouldNotHaveError()
    {
        var request = new LoginRequest("johndoe", new string('A', 200));
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(r => r.Password);
    }

    [Fact]
    public void Validate_WithNullUsername_ShouldHaveError()
    {
        var request = new LoginRequest(null!, "Str0ng!Pass");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(r => r.Username)
            .WithErrorMessage("Username is required.");
    }

    [Fact]
    public void Validate_WithNullPassword_ShouldHaveError()
    {
        var request = new LoginRequest("johndoe", null!);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(r => r.Password)
            .WithErrorMessage("Password is required.");
    }

    [Fact]
    public void Validate_WithDeviceInfo_ShouldNotFail()
    {
        var request = new LoginRequest("johndoe", "Str0ng!Pass", DeviceInfo: "Mozilla/5.0", IpAddress: null);
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithIpAddress_ShouldNotFail()
    {
        var request = new LoginRequest("johndoe", "Str0ng!Pass", DeviceInfo: null, IpAddress: "192.168.1.1");
        var result = _validator.TestValidate(request);
        result.IsValid.Should().BeTrue();
    }
}
