using FluentValidation.TestHelper;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Validators;

namespace His.Hope.Validators;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    private LoginRequest ValidCommand => new(
        Username: "john.doe",
        Password: "Password123!");

    [Fact]
    public void ValidCommand_ShouldNotHaveErrors()
    {
        _validator.TestValidate(ValidCommand).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyUsername_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Username = "" })
            .ShouldHaveValidationErrorFor(c => c.Username);

    [Fact]
    public void UsernameOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Username = new string('U', 101) })
            .ShouldHaveValidationErrorFor(c => c.Username);

    [Fact]
    public void EmptyPassword_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Password = "" })
            .ShouldHaveValidationErrorFor(c => c.Password);

    [Fact]
    public void PasswordTooShort_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Password = "12345" })
            .ShouldHaveValidationErrorFor(c => c.Password);

    [Fact]
    public void PasswordOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Password = new string('P', 201) })
            .ShouldHaveValidationErrorFor(c => c.Password);

    [Fact]
    public void PasswordMinLength_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { Password = "123456" })
            .ShouldNotHaveValidationErrorFor(c => c.Password);

    [Fact]
    public void UsernameBorderline_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { Username = new string('U', 100) })
            .ShouldNotHaveValidationErrorFor(c => c.Username);
}
