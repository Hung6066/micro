using FluentAssertions;
using His.Hope.IdentityService.Domain.Entities;

namespace His.Hope.IdentityService.Domain.Tests;

public class UserTests
{
    [Fact]
    public void Create_WithDefaultValues_ShouldHaveValidState()
    {
        // Arrange & Act
        var user = new User
        {
            FirstName = "John",
            LastName = "Doe",
            UserName = "johndoe",
            Email = "john.doe@example.com"
        };

        // Assert
        user.Should().NotBeNull();
        user.Id.Should().NotBeEmpty();
        user.FirstName.Should().Be("John");
        user.LastName.Should().Be("Doe");
        user.UserName.Should().Be("johndoe");
        user.Email.Should().Be("john.doe@example.com");
        user.IsActive.Should().BeTrue();
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        user.LastLoginAt.Should().BeNull();
        user.MiddleName.Should().BeNull();
        user.LicenseNumber.Should().BeNull();
        user.Specialty.Should().BeNull();
    }

    [Fact]
    public void FullName_WithFirstAndLastOnly_ShouldReturnLastNameSpaceFirstName()
    {
        // Arrange
        var user = new User
        {
            FirstName = "Jane",
            LastName = "Smith"
        };

        // Act
        var fullName = user.FullName;

        // Assert
        fullName.Should().Be("Smith Jane");
    }

    [Fact]
    public void FullName_WithMiddleName_ShouldReturnLastNameMiddleNameFirstName()
    {
        // Arrange
        var user = new User
        {
            FirstName = "Jane",
            LastName = "Smith",
            MiddleName = "Marie"
        };

        // Act
        var fullName = user.FullName;

        // Assert
        fullName.Should().Be("Smith Marie Jane");
    }

    [Fact]
    public void FullName_WithEmptyMiddleName_ShouldReturnLastNameSpaceFirstName()
    {
        // Arrange
        var user = new User
        {
            FirstName = "John",
            LastName = "Doe",
            MiddleName = string.Empty
        };

        // Act
        var fullName = user.FullName;

        // Assert
        fullName.Should().Be("Doe John");
    }

    [Fact]
    public void FullName_WithWhitespaceMiddleName_ShouldReturnLastNameSpaceFirstName()
    {
        // Arrange
        var user = new User
        {
            FirstName = "Alice",
            LastName = "Johnson",
            MiddleName = "   "
        };

        // Act
        var fullName = user.FullName;

        // Assert
        fullName.Should().Be("Johnson Alice");
    }

    [Fact]
    public void IsActive_DefaultValue_ShouldBeTrue()
    {
        // Arrange & Act
        var user = new User();

        // Assert
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void SetIsActive_ToFalse_ShouldDeactivateUser()
    {
        // Arrange
        var user = new User();

        // Act
        user.IsActive = false;

        // Assert
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void CreatedAt_Default_ShouldBeUtcNow()
    {
        // Arrange & Act
        var user = new User();

        // Assert
        user.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void LastLoginAt_InitiallyNull_ShouldAllowSetting()
    {
        // Arrange
        var user = new User();
        var loginTime = DateTime.UtcNow;

        // Act
        user.LastLoginAt = loginTime;

        // Assert
        user.LastLoginAt.Should().Be(loginTime);
    }

    [Fact]
    public void UserName_CanBeSet_ShouldReflectValue()
    {
        // Arrange
        var user = new User();

        // Act
        user.UserName = "dr.smith";

        // Assert
        user.UserName.Should().Be("dr.smith");
    }

    [Fact]
    public void Email_CanBeSet_ShouldReflectValue()
    {
        // Arrange
        var user = new User();

        // Act
        user.Email = "dr.smith@hospital.com";

        // Assert
        user.Email.Should().Be("dr.smith@hospital.com");
    }

    [Fact]
    public void LicenseNumber_CanBeSet_ShouldReflectValue()
    {
        // Arrange
        var user = new User();

        // Act
        user.LicenseNumber = "LIC-12345";

        // Assert
        user.LicenseNumber.Should().Be("LIC-12345");
    }

    [Fact]
    public void Specialty_CanBeSet_ShouldReflectValue()
    {
        // Arrange
        var user = new User();

        // Act
        user.Specialty = "Cardiology";

        // Assert
        user.Specialty.Should().Be("Cardiology");
    }

    [Fact]
    public void TwoUsers_WithSameId_ShouldBeEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var user1 = new User { Id = id.ToString() };
        var user2 = new User { Id = id.ToString() };

        // Assert
        user1.Equals(user2).Should().BeTrue();
        (user1 == user2).Should().BeTrue();
    }

    [Fact]
    public void TwoUsers_WithDifferentIds_ShouldNotBeEqual()
    {
        // Arrange
        var user1 = new User { Id = Guid.NewGuid().ToString() };
        var user2 = new User { Id = Guid.NewGuid().ToString() };

        // Assert
        user1.Equals(user2).Should().BeFalse();
        (user1 != user2).Should().BeTrue();
    }

    [Fact]
    public void Role_WithDescription_ShouldCreateValidRole()
    {
        // Arrange & Act
        var role = new Role
        {
            Name = "Doctor",
            Description = "Medical practitioner with prescribing authority"
        };

        // Assert
        role.Name.Should().Be("Doctor");
        role.Description.Should().Be("Medical practitioner with prescribing authority");
        role.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Role_WithoutDescription_ShouldHaveNullDescription()
    {
        // Arrange & Act
        var role = new Role { Name = "Nurse" };

        // Assert
        role.Name.Should().Be("Nurse");
        role.Description.Should().BeNull();
    }

    [Theory]
    [InlineData(null, null, null, "")]
    [InlineData("John", "Doe", null, "Doe John")]
    [InlineData("John", "Doe", "", "Doe John")]
    [InlineData("A", "B", "C", "B C A")]
    [InlineData("Alice", "Wonderland", "In", "Wonderland In Alice")]
    public void FullName_VariousCombinations_ShouldReturnExpected(
        string firstName, string lastName, string? middleName, string expected)
    {
        // Arrange
        var user = new User
        {
            FirstName = firstName ?? string.Empty,
            LastName = lastName ?? string.Empty,
            MiddleName = middleName
        };

        // Act
        var fullName = user.FullName;

        // Assert
        fullName.Should().Be(expected);
    }

    [Fact]
    public void User_ShouldImplementIEquatable()
    {
        // Arrange
        var user = new User();
        var other = new User();

        // Act
        var result = user.Equals(other);

        // Assert
        // Should not throw - IdentityUser<T> overrides Equals
        result.Should().Be(user.Id == other.Id ? (bool?)true : false);
    }
}
