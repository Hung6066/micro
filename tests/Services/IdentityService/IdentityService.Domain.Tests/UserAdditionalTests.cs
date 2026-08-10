using FluentAssertions;
using His.Hope.IdentityService.Domain.Entities;

namespace His.Hope.IdentityService.Domain.Tests;

public class UserAdditionalTests
{
    [Fact]
    public void User_WithAllProperties_ShouldSetCorrectly()
    {
        var user = new User
        {
            FirstName = "Alice",
            LastName = "Johnson",
            MiddleName = "Marie",
            UserName = "alice.johnson",
            Email = "alice@hospital.com",
            LicenseNumber = "LIC-12345",
            Specialty = "Cardiology",
            IsActive = true,
            LastLoginAt = new DateTime(2024, 6, 15, 8, 30, 0, DateTimeKind.Utc)
        };

        user.LicenseNumber.Should().Be("LIC-12345");
        user.Specialty.Should().Be("Cardiology");
        user.LastLoginAt.Should().Be(new DateTime(2024, 6, 15, 8, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void User_WithEmptyMiddleName_ShouldReturnCorrectFullName()
    {
        var user = new User
        {
            FirstName = "Bob",
            LastName = "Williams",
            MiddleName = string.Empty
        };

        user.FullName.Should().Be("Williams Bob");
    }

    [Fact]
    public void User_WithNullSpecialtyAndLicense_ShouldHandleGracefully()
    {
        var user = new User
        {
            FirstName = "Test",
            LastName = "User",
            UserName = "testuser"
        };

        user.Specialty.Should().BeNull();
        user.LicenseNumber.Should().BeNull();
    }

    [Fact]
    public void User_WithEmail_ShouldSetCorrectly()
    {
        var user = new User
        {
            FirstName = "Email",
            LastName = "Test",
            UserName = "emailtest",
            Email = "email.test@hospital.com"
        };

        user.Email.Should().Be("email.test@hospital.com");
    }

    [Fact]
    public void User_WithPhoneNumber_ShouldSetCorrectly()
    {
        var user = new User
        {
            FirstName = "Phone",
            LastName = "Test",
            UserName = "phonetest",
            PhoneNumber = "+1234567890"
        };

        user.PhoneNumber.Should().Be("+1234567890");
    }

    [Fact]
    public void User_IsActive_CanToggle()
    {
        var user = new User
        {
            FirstName = "Toggle",
            LastName = "Test",
            UserName = "toggletest"
        };

        user.IsActive.Should().BeTrue();
        user.IsActive = false;
        user.IsActive.Should().BeFalse();
        user.IsActive = true;
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void User_LastLoginAt_ShouldTrackLogin()
    {
        var user = new User
        {
            FirstName = "Login",
            LastName = "Test",
            UserName = "logintest"
        };

        user.LastLoginAt.Should().BeNull();

        var loginTime = DateTime.UtcNow;
        user.LastLoginAt = loginTime;

        user.LastLoginAt.Should().Be(loginTime);
    }

    [Fact]
    public void User_InheritsFromIdentityUser()
    {
        var user = new User();

        user.Should().BeAssignableTo<Microsoft.AspNetCore.Identity.IdentityUser<Guid>>();
    }

    [Fact]
    public void User_DefaultValues_ShouldBeSet()
    {
        var user = new User();

        user.FirstName.Should().Be(string.Empty);
        user.LastName.Should().Be(string.Empty);
        user.IsActive.Should().BeTrue();
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void User_WithConcurrencyStamp_ShouldBeSet()
    {
        var user = new User
        {
            FirstName = "Concurrency",
            LastName = "Test",
            UserName = "concurrency"
        };

        user.ConcurrencyStamp.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void User_SecurityStamp_ShouldBeSet()
    {
        var user = new User
        {
            FirstName = "Security",
            LastName = "Test",
            UserName = "security"
        };

        user.SecurityStamp.Should().NotBeNullOrEmpty();
    }
}
