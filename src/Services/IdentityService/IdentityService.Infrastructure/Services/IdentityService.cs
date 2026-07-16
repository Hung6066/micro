using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Infrastructure.Services;

public class IdentityService : IIdentityService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly IdentityDbContext _context;
    private readonly JwtTokenGenerator _tokenGenerator;
    private readonly RedisRefreshTokenStore _refreshTokenStore;
    private readonly ILogger<IdentityService> _logger;

    public IdentityService(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        IdentityDbContext context,
        JwtTokenGenerator tokenGenerator,
        RedisRefreshTokenStore refreshTokenStore,
        ILogger<IdentityService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _tokenGenerator = tokenGenerator;
        _refreshTokenStore = refreshTokenStore;
        _logger = logger;
    }

    /// <summary>
    /// Loads permission codes for the given set of role names.
    /// Queries the RolePermission join table via the IdentityDbContext.
    /// </summary>
    private async Task<List<string>> GetPermissionsForRolesAsync(IEnumerable<string> roleNames, CancellationToken ct = default)
    {
        var roleIds = await _context.Roles
            .Where(r => roleNames.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (roleIds.Count == 0)
            return new List<string>();

        var permissions = await _context.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.PermissionCode)
            .Distinct()
            .ToListAsync(ct);

        return permissions;
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByNameAsync(request.Username)
                   ?? await _userManager.FindByEmailAsync(request.Username);

        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            _logger.LogWarning("Failed login attempt: Username={Username}", request.Username);
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login attempt on deactivated account: UserId={UserId}", user.Id);
            throw new UnauthorizedAccessException("Account is deactivated.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsForRolesAsync(roles, cancellationToken);
        var (accessToken, expiresAt) = _tokenGenerator.GenerateAccessToken(user, roles, permissions);
        var refreshTokenValue = _tokenGenerator.GenerateRefreshToken();

        var familyId = RedisRefreshTokenStore.GenerateFamilyId();
        var refreshTokenRecord = new RefreshTokenRecord
        {
            UserId = user.Id.ToString(),
            TokenHash = RefreshTokenRecord.ComputeHash(refreshTokenValue),
            FamilyId = familyId,
            Generation = 0,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            DeviceInfo = request.DeviceInfo,
            IpAddress = request.IpAddress
        };

        await _refreshTokenStore.StoreAsync(refreshTokenRecord, cancellationToken);

        _logger.LogInformation(
            "User logged in: UserId={UserId}, Roles={Roles}, Permissions={PermissionCount}, FamilyId={FamilyId}",
            user.Id, string.Join(",", roles), permissions.Count, familyId);

        return new TokenResponse(
            accessToken, refreshTokenValue, expiresAt, MapToDto(user, roles));
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
        var permissions = await GetPermissionsForRolesAsync(roles, cancellationToken);
        var (accessToken, expiresAt) = _tokenGenerator.GenerateAccessToken(user, roles, permissions);
        var refreshTokenValue = _tokenGenerator.GenerateRefreshToken();

        var familyId = RedisRefreshTokenStore.GenerateFamilyId();
        var refreshTokenRecord = new RefreshTokenRecord
        {
            UserId = user.Id.ToString(),
            TokenHash = RefreshTokenRecord.ComputeHash(refreshTokenValue),
            FamilyId = familyId,
            Generation = 0,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            DeviceInfo = request.DeviceInfo,
            IpAddress = request.IpAddress
        };

        await _refreshTokenStore.StoreAsync(refreshTokenRecord, cancellationToken);

        _logger.LogInformation(
            "User registered: UserId={UserId}, Username={Username}",
            user.Id, request.Username);

        return new TokenResponse(accessToken, refreshTokenValue, expiresAt, MapToDto(user, roles));
    }

    public async Task<TokenResponse> RefreshTokenAsync(RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var principal = _tokenGenerator.GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal is null)
            throw new UnauthorizedAccessException("Invalid access token.");

        var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
            throw new UnauthorizedAccessException("Invalid access token.");

        // Token reuse detection
        var (wasReused, familyId) = await _refreshTokenStore
            .DetectReuseAsync(request.RefreshToken, cancellationToken);

        if (wasReused)
        {
            _logger.LogCritical(
                "Refresh token reuse detected for UserId={UserId}, FamilyId={FamilyId}. " +
                "This indicates token theft! All tokens in family revoked.",
                userId, familyId);
            throw new UnauthorizedAccessException(
                "Security event detected. Please login again.");
        }

        if (familyId is not null)
        {
            var isFamilyRevoked = await _refreshTokenStore
                .IsFamilyRevokedAsync(familyId, cancellationToken);

            if (isFamilyRevoked)
                throw new UnauthorizedAccessException(
                    "Security event detected. Please login again.");
        }

        var existingRecord = await _refreshTokenStore
            .GetByTokenAsync(request.RefreshToken, cancellationToken);

        if (existingRecord is null)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        if (existingRecord.UserId != userId)
            throw new UnauthorizedAccessException("Refresh token does not match user.");

        if (existingRecord.IsRevoked)
            throw new UnauthorizedAccessException("Refresh token has been revoked.");

        if (existingRecord.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token has expired.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || !user.IsActive)
            throw new UnauthorizedAccessException("User not found or deactivated.");

        // Token rotation: invalidate old, issue new
        await _refreshTokenStore.InvalidateAsync(request.RefreshToken, cancellationToken);

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsForRolesAsync(roles, cancellationToken);
        var (accessToken, expiresAt) = _tokenGenerator.GenerateAccessToken(user, roles, permissions);
        var newRefreshTokenValue = _tokenGenerator.GenerateRefreshToken();

        var newRecord = new RefreshTokenRecord
        {
            UserId = userId,
            TokenHash = RefreshTokenRecord.ComputeHash(newRefreshTokenValue),
            FamilyId = familyId ?? RedisRefreshTokenStore.GenerateFamilyId(),
            Generation = (existingRecord.Generation + 1),
            PreviousTokenHash = existingRecord.TokenHash,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            DeviceInfo = request.DeviceInfo,
            IpAddress = request.IpAddress
        };

        await _refreshTokenStore.StoreAsync(newRecord, cancellationToken);

        _logger.LogInformation(
            "Token refreshed: UserId={UserId}, FamilyId={FamilyId}, Generation={Gen}",
            userId, newRecord.FamilyId, newRecord.Generation);

        return new TokenResponse(accessToken, newRefreshTokenValue, expiresAt, MapToDto(user, roles));
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

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        await _refreshTokenStore.RevokeAsync(refreshToken, cancellationToken);
        _logger.LogInformation("User logout - refresh token revoked");
    }

    private static UserDto MapToDto(User user, IList<string> roles) => new(
        user.Id, user.UserName!, user.Email!,
        user.FirstName, user.LastName, user.MiddleName,
        user.FullName, user.LicenseNumber, user.Specialty, roles);
}
