using FluentValidation.TestHelper;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Validators;

namespace His.Hope.Validators;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    private RegisterRequest ValidCommand => new(
        Username: "john_doe",
        Email: "john@example.com",
        Password: "Password123!",
        FirstName: "John",
        LastName: "Doe",
        MiddleName: "M",
        LicenseNumber: "LIC-12345",
        Specialty: "Cardiology");

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
    public void UsernameTooShort_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Username = "ab" })
            .ShouldHaveValidationErrorFor(c => c.Username);

    [Fact]
    public void UsernameWithInvalidCharacters_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Username = "user name!" })
            .ShouldHaveValidationErrorFor(c => c.Username);

    [Fact]
    public void EmptyEmail_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Email = "" })
            .ShouldHaveValidationErrorFor(c => c.Email);

    [Fact]
    public void InvalidEmail_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Email = "not-an-email" })
            .ShouldHaveValidationErrorFor(c => c.Email);

    [Fact]
    public void EmptyPassword_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Password = "" })
            .ShouldHaveValidationErrorFor(c => c.Password);

    [Fact]
    public void PasswordTooShort_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Password = "Abc1!" })
            .ShouldHaveValidationErrorFor(c => c.Password);

    [Fact]
    public void PasswordMissingUppercase_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Password = "abcdef1!@#" })
            .ShouldHaveValidationErrorFor(c => c.Password);

    [Fact]
    public void PasswordMissingLowercase_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Password = "ABCDEF1!@#" })
            .ShouldHaveValidationErrorFor(c => c.Password);

    [Fact]
    public void PasswordMissingDigit_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Password = "Abcdef!@#" })
            .ShouldHaveValidationErrorFor(c => c.Password);

    [Fact]
    public void PasswordMissingSpecialChar_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Password = "Abcdef1" })
            .ShouldHaveValidationErrorFor(c => c.Password);

    [Fact]
    public void EmptyFirstName_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { FirstName = "" })
            .ShouldHaveValidationErrorFor(c => c.FirstName);

    [Fact]
    public void EmptyLastName_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { LastName = "" })
            .ShouldHaveValidationErrorFor(c => c.LastName);

    [Fact]
    public void LicenseNumberOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { LicenseNumber = new string('L', 51) })
            .ShouldHaveValidationErrorFor(c => c.LicenseNumber);

    [Fact]
    public void SpecialtyOverMax_ShouldHaveError() =>
        _validator.TestValidate(ValidCommand with { Specialty = new string('S', 201) })
            .ShouldHaveValidationErrorFor(c => c.Specialty);

    [Fact]
    public void NullLicenseNumber_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { LicenseNumber = null })
            .ShouldNotHaveValidationErrorFor(c => c.LicenseNumber);

    [Fact]
    public void NullSpecialty_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { Specialty = null })
            .ShouldNotHaveValidationErrorFor(c => c.Specialty);

    [Fact]
    public void NullMiddleName_ShouldNotHaveError() =>
        _validator.TestValidate(ValidCommand with { MiddleName = null })
            .ShouldNotHaveValidationErrorFor(c => c.MiddleName);

    [Fact]
    public void StrongPassword_ShouldNotHaveErrors()
    {
        var strongPasswords = new[]
        {
            "Str0ng!Pass",
            "C0mpl3x!ty#1",
            "P@ssw0rdXyz"
        };

        foreach (var password in strongPasswords)
        {
            _validator.TestValidate(ValidCommand with { Password = password })
                .ShouldNotHaveValidationErrorFor(c => c.Password);
        }
    }
}
