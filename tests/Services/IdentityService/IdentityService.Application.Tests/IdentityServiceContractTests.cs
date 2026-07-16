using FluentAssertions;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using Moq;

namespace His.Hope.IdentityService.Application.Tests;

/// <summary>
/// Tests the identity service interface contract by verifying that
/// implementations return expected response types and handle error cases.
/// Uses mocks to verify the service contract (method signatures, return types).
/// </summary>
public class IdentityServiceContractTests
{
    private readonly Mock<IIdentityService> _mockService;

    public IdentityServiceContractTests()
    {
        _mockService = new Mock<IIdentityService>();
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ShouldReturnTokenResponse()
    {
        // Arrange
        var request = new LoginRequest("admin", "Test@123");
        var expectedResponse = new TokenResponse(
            AccessToken: "eyJhbGciOiJSUzI1NiIsImtpZCI6...",
            RefreshToken: "dGhpcyBpcyBhIHJlZnJl...",
            ExpiresAt: DateTime.UtcNow.AddHours(1),
            User: new UserDto(
                Id: Guid.NewGuid(),
                Username: "admin",
                Email: "admin@hospital.com",
                FirstName: "System",
                LastName: "Admin",
                MiddleName: null,
                FullName: "Admin System",
                LicenseNumber: null,
                Specialty: null,
                Roles: new List<string> { "Admin" }));

        _mockService
            .Setup(s => s.LoginAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _mockService.Object.LoginAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        result.User.Should().NotBeNull();
        result.User.Username.Should().Be("admin");
        result.User.Roles.Should().Contain("Admin");
    }

    [Fact]
    public async Task RegisterAsync_WithValidRequest_ShouldReturnTokenResponse()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "newdoctor",
            Email: "newdoctor@hospital.com",
            Password: "Str0ng!Pass",
            FirstName: "New",
            LastName: "Doctor",
            MiddleName: null,
            LicenseNumber: "LIC-99999",
            Specialty: "Neurology");

        var expectedResponse = new TokenResponse(
            AccessToken: "new-access-token",
            RefreshToken: "new-refresh-token",
            ExpiresAt: DateTime.UtcNow.AddHours(1),
            User: new UserDto(
                Id: Guid.NewGuid(),
                Username: "newdoctor",
                Email: "newdoctor@hospital.com",
                FirstName: "New",
                LastName: "Doctor",
                MiddleName: null,
                FullName: "Doctor New",
                LicenseNumber: "LIC-99999",
                Specialty: "Neurology",
                Roles: new List<string> { "Doctor" }));

        _mockService
            .Setup(s => s.RegisterAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _mockService.Object.RegisterAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.User.Username.Should().Be("newdoctor");
        result.User.LicenseNumber.Should().Be("LIC-99999");
        result.User.Specialty.Should().Be("Neurology");
        result.User.Roles.Should().Contain("Doctor");
    }

    [Fact]
    public async Task LoginAsync_WithInvalidCredentials_ShouldThrowException()
    {
        // Arrange
        var request = new LoginRequest("invalid", "wrongpass");

        _mockService
            .Setup(s => s.LoginAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid credentials"));

        // Act
        var act = async () => await _mockService.Object.LoginAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid credentials");
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateUsername_ShouldThrowException()
    {
        // Arrange
        var request = new RegisterRequest(
            Username: "existing",
            Email: "existing@hospital.com",
            Password: "Str0ng!Pass",
            FirstName: "Existing",
            LastName: "User",
            MiddleName: null,
            LicenseNumber: null,
            Specialty: null);

        _mockService
            .Setup(s => s.RegisterAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Username already exists."));

        // Act
        var act = async () => await _mockService.Object.RegisterAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Username already exists.");
    }

    [Fact]
    public async Task GetUserByIdAsync_WithExistingId_ShouldReturnUserDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedUser = new UserDto(
            Id: userId,
            Username: "johndoe",
            Email: "john.doe@hospital.com",
            FirstName: "John",
            LastName: "Doe",
            MiddleName: null,
            FullName: "Doe John",
            LicenseNumber: "LIC-54321",
            Specialty: "Pediatrics",
            Roles: new List<string> { "Doctor" });

        _mockService
            .Setup(s => s.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUser);

        // Act
        var result = await _mockService.Object.GetUserByIdAsync(userId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(userId);
        result.Username.Should().Be("johndoe");
        result.Email.Should().Be("john.doe@hospital.com");
        result.Roles.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetUserByIdAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _mockService
            .Setup(s => s.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDto?)null);

        // Act
        var result = await _mockService.Object.GetUserByIdAsync(userId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshTokenAsync_WithValidToken_ShouldReturnNewTokenResponse()
    {
        // Arrange
        var request = new RefreshTokenRequest(
            AccessToken: "old-access-token",
            RefreshToken: "valid-refresh-token");

        var expectedResponse = new TokenResponse(
            AccessToken: "new-access-token",
            RefreshToken: "new-refresh-token",
            ExpiresAt: DateTime.UtcNow.AddHours(2),
            User: new UserDto(
                Id: Guid.NewGuid(),
                Username: "johndoe",
                Email: "john.doe@hospital.com",
                FirstName: "John",
                LastName: "Doe",
                MiddleName: null,
                FullName: "Doe John",
                LicenseNumber: null,
                Specialty: null,
                Roles: new List<string>()));

        _mockService
            .Setup(s => s.RefreshTokenAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _mockService.Object.RefreshTokenAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("new-access-token");
        result.RefreshToken.Should().Be("new-refresh-token");
    }

    [Fact]
    public async Task RefreshTokenAsync_WithExpiredToken_ShouldThrowException()
    {
        // Arrange
        var request = new RefreshTokenRequest(
            AccessToken: "expired-token",
            RefreshToken: "expired-refresh-token");

        _mockService
            .Setup(s => s.RefreshTokenAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Token has expired."));

        // Act
        var act = async () => await _mockService.Object.RefreshTokenAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Token has expired.");
    }

    [Fact]
    public async Task LogoutAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var refreshToken = "some-refresh-token";

        _mockService
            .Setup(s => s.LogoutAsync(refreshToken, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _mockService.Object.LogoutAsync(refreshToken, CancellationToken.None);

        // Assert
        _mockService.Verify(s => s.LogoutAsync(refreshToken, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TokenResponse_ShouldHaveCorrectStructure()
    {
        // Arrange
        var tokenResponse = new TokenResponse(
            AccessToken: "token",
            RefreshToken: "refresh",
            ExpiresAt: DateTime.UtcNow,
            User: new UserDto(
                Id: Guid.NewGuid(),
                Username: "test",
                Email: "test@test.com",
                FirstName: "Test",
                LastName: "User",
                MiddleName: null,
                FullName: "User Test",
                LicenseNumber: null,
                Specialty: null,
                Roles: new List<string>()));

        // Assert
        tokenResponse.AccessToken.Should().Be("token");
        tokenResponse.RefreshToken.Should().Be("refresh");
        tokenResponse.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        tokenResponse.User.Username.Should().Be("test");
        tokenResponse.User.FullName.Should().Be("User Test");
    }

    [Fact]
    public async Task UserDto_WithRoles_ShouldContainRoles()
    {
        // Arrange
        var roles = new List<string> { "Admin", "Doctor", "Nurse" };
        var user = new UserDto(
            Id: Guid.NewGuid(),
            Username: "multirole",
            Email: "multi@test.com",
            FirstName: "Multi",
            LastName: "Role",
            MiddleName: null,
            FullName: "Role Multi",
            LicenseNumber: null,
            Specialty: null,
            Roles: roles);

        // Assert
        user.Roles.Should().HaveCount(3);
        user.Roles.Should().Contain(new[] { "Admin", "Doctor", "Nurse" });
    }
}
