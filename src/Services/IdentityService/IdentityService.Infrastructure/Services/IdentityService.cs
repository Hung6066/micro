using System.Collections.Concurrent;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Infrastructure.Services;

public class IdentityService : IIdentityService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly IdentityDbContext _context;
    private readonly JwtTokenGenerator _tokenGenerator;
    private static readonly ConcurrentDictionary<string, string> _refreshTokens = new();

    public IdentityService(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        IdentityDbContext context,
        JwtTokenGenerator tokenGenerator)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByNameAsync(request.Username)
                   ?? await _userManager.FindByEmailAsync(request.Username);

        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
            throw new UnauthorizedAccessException("Invalid username or password.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is deactivated.");

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var (accessToken, expiresAt) = _tokenGenerator.GenerateAccessToken(user, roles);
        var refreshToken = _tokenGenerator.GenerateRefreshToken();

        _refreshTokens[refreshToken] = user.Id.ToString();

        return new TokenResponse(accessToken, refreshToken, expiresAt, MapToDto(user, roles));
    }

    public async Task<TokenResponse> RegisterAsync(RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var existingUser = await _userManager.FindByNameAsync(request.Username);
        if (existingUser is not null)
            throw new InvalidOperationException("Username already exists.");

        var existingEmail = await _userManager.FindByEmailAsync(request.Email);
        if (existingEmail is not null)
            throw new InvalidOperationException("Email already registered.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = request.Username,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            MiddleName = request.MiddleName,
            LicenseNumber = request.LicenseNumber,
            Specialty = request.Specialty,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Registration failed: {errors}");
        }

        await _userManager.AddToRoleAsync(user, "Provider");

        var roles = await _userManager.GetRolesAsync(user);
        var (accessToken, expiresAt) = _tokenGenerator.GenerateAccessToken(user, roles);
        var refreshToken = _tokenGenerator.GenerateRefreshToken();

        _refreshTokens[refreshToken] = user.Id.ToString();

        return new TokenResponse(accessToken, refreshToken, expiresAt, MapToDto(user, roles));
    }

    public async Task<TokenResponse> RefreshTokenAsync(RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var principal = _tokenGenerator.GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal is null)
            throw new UnauthorizedAccessException("Invalid access token.");

        var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is null || !_refreshTokens.TryRemove(request.RefreshToken, out var storedUserId) ||
            storedUserId != userId)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || !user.IsActive)
            throw new UnauthorizedAccessException("User not found or deactivated.");

        var roles = await _userManager.GetRolesAsync(user);
        var (accessToken, expiresAt) = _tokenGenerator.GenerateAccessToken(user, roles);
        var newRefreshToken = _tokenGenerator.GenerateRefreshToken();

        _refreshTokens[newRefreshToken] = userId;

        return new TokenResponse(accessToken, newRefreshToken, expiresAt, MapToDto(user, roles));
    }

    public async Task<UserDto> GetUserByIdAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            throw new KeyNotFoundException("User not found.");

        var roles = await _userManager.GetRolesAsync(user);
        return MapToDto(user, roles);
    }

    public Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        _refreshTokens.TryRemove(refreshToken, out _);
        return Task.CompletedTask;
    }

    private static UserDto MapToDto(User user, IList<string> roles) => new(
        user.Id, user.UserName!, user.Email!,
        user.FirstName, user.LastName, user.MiddleName,
        user.FullName, user.LicenseNumber, user.Specialty, roles);
}
