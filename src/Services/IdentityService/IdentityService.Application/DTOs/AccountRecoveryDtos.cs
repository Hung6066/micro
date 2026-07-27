namespace His.Hope.IdentityService.Application.DTOs;

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record VerifyEmailRequest(string Email, string Token);

public record SessionInfo(
    string SessionId,
    string? DeviceInfo,
    string? IpAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivity,
    bool IsCurrent);
