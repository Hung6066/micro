namespace His.Hope.IdentityService.Application.DTOs;

public record LoginRequest(
    string? Username = null,
    string? Email = null,
    string? Password = null,
    string? DeviceInfo = null,
    string? IpAddress = null,
    string? UserAgent = null);

public record RegisterRequest(
    string? Username = null,
    string? Email = null,
    string? Password = null,
    string? FirstName = null,
    string? LastName = null,
    string? MiddleName = null,
    string? LicenseNumber = null,
    string? Specialty = null,
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
    IList<string> Roles,
    IList<string>? Permissions = null);

public record MfaEnrollResponse(
    string SecretKey,
    string QrCodeUri,
    string[] RecoveryCodes);

public record MfaVerifyRequest(
    string Code);

public record MfaVerifyResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User);

public record MfaRecoverRequest(
    string RecoveryCode);
