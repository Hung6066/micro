namespace His.Hope.IdentityService.Application.DTOs;

public record LoginRequest(
    string Username,
    string Password,
    string? DeviceInfo = null,
    string? IpAddress = null);

public record RegisterRequest(
    string Username,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? LicenseNumber,
    string? Specialty,
    string? DeviceInfo = null,
    string? IpAddress = null);

public record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User);

public record RefreshTokenRequest(
    string AccessToken,
    string RefreshToken,
    string? DeviceInfo = null,
    string? IpAddress = null);

public record UserDto(
    Guid Id,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string? MiddleName,
    string FullName,
    string? LicenseNumber,
    string? Specialty,
    IList<string> Roles);
