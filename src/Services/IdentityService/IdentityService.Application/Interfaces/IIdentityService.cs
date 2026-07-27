using His.Hope.IdentityService.Application.DTOs;

namespace His.Hope.IdentityService.Application.Interfaces;

public interface IIdentityService
{
    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<TokenResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<TokenResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);

    // Password recovery
    Task<string> GeneratePasswordResetTokenAsync(string email);
    Task ResetPasswordAsync(string email, string token, string newPassword);
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);

    // Email verification
    Task<string> GenerateEmailConfirmationTokenAsync(Guid userId);
    Task ConfirmEmailAsync(string email, string token);
}
