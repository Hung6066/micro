using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _configuration;

    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // SECURITY: Known patterns suggesting credential stuffing / brute force
    private static readonly string[] SuspiciousIpPatterns = { "tor", "proxy", "vpn" };

    public IdentityService(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        IdentityDbContext context,
        JwtTokenGenerator tokenGenerator,
        RedisRefreshTokenStore refreshTokenStore,
        ILogger<IdentityService> logger,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _tokenGenerator = tokenGenerator;
        _refreshTokenStore = refreshTokenStore;
        _logger = logger;
        _configuration = configuration;
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
        var identifier = request.Username ?? request.Email
            ?? throw new UnauthorizedAccessException("Username or email is required.");
        var user = await _userManager.FindByNameAsync(identifier)
                   ?? await _userManager.FindByEmailAsync(identifier);

        // SECURITY: Account lockout check — prevent brute force
        if (user is { LockoutEnd: not null } && user.LockoutEnd > DateTime.UtcNow)
        {
            var remaining = user.LockoutEnd.Value - DateTime.UtcNow;
            await LogSecurityEventAsync(user.Id, user.UserName!, "lockout_active",
                "critical", request.IpAddress, request.UserAgent, request.DeviceInfo,
                $"Account locked. Remaining: {remaining.TotalMinutes:F1}min");

            _logger.LogWarning("Locked account login attempt: UserId={UserId}, IP={IP}, Remaining={Remaining}min",
                user.Id, request.IpAddress, remaining.TotalMinutes);
            throw new UnauthorizedAccessException(
                $"Account temporarily locked. Try again in {remaining.TotalMinutes:F0} minutes.");
        }

        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            // SECURITY: Record failed attempt, then check lockout
            if (user is not null)
            {
                user.FailedLoginAttempts++;
                await _userManager.UpdateAsync(user);

                await LogSecurityEventAsync(user.Id, user.UserName!, "login_failed",
                    "warning", request.IpAddress, request.UserAgent, request.DeviceInfo,
                    $"Attempt {user.FailedLoginAttempts}/{MaxFailedAttempts}");
            }

            // SECURITY: Check if account should be locked after this failure
            if (user is { FailedLoginAttempts: >= MaxFailedAttempts })
            {
                user.LockoutEnd = DateTime.UtcNow.Add(LockoutDuration);
                user.FailedLoginAttempts = 0;
                await _userManager.UpdateAsync(user);

                await LogSecurityEventAsync(user.Id, user.UserName!, "account_locked",
                    "critical", request.IpAddress, request.UserAgent, request.DeviceInfo,
                    $"Account locked after {MaxFailedAttempts} failed attempts");

                _logger.LogCritical("Account locked due to brute force: UserId={UserId}, IP={IP}, Duration={Duration}min",
                    user.Id, request.IpAddress, LockoutDuration.TotalMinutes);
            }

            _logger.LogWarning("Failed login attempt: Username={Username}, IP={IP}, UserAgent={UA}",
                request.Username, request.IpAddress, request.UserAgent);
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            await LogSecurityEventAsync(user.Id, user.UserName!, "deactivated_login_attempt",
                "warning", request.IpAddress, request.UserAgent, request.DeviceInfo,
                "Attempted login on deactivated account");
            _logger.LogWarning("Login attempt on deactivated account: UserId={UserId}", user.Id);
            throw new UnauthorizedAccessException("Account is deactivated.");
        }

        // SECURITY: Check if password change is required
        var passwordMaxAgeDays = _configuration.GetValue("Security:PasswordMaxAgeDays", 90);
        if (user.LastPasswordChangedAt.HasValue &&
            (DateTime.UtcNow - user.LastPasswordChangedAt.Value).TotalDays > passwordMaxAgeDays)
        {
            _logger.LogInformation("Password expired for UserId={UserId}, requiring change", user.Id);
            // Not blocking login — will be handled by client-side force-change
        }

        // SECURITY: Reset lockout counters on successful login
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
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

        // SECURITY: Log successful login event
        await LogSecurityEventAsync(user.Id, user.UserName!, "login_success",
            "info", request.IpAddress, request.UserAgent, request.DeviceInfo,
            $"Roles: {string.Join(",", roles)}, MFA: false");

        _logger.LogInformation(
            "User logged in: UserId={UserId}, Roles={Roles}, Permissions={PermissionCount}, FamilyId={FamilyId}, IP={IP}",
            user.Id, string.Join(",", roles), permissions.Count, familyId, request.IpAddress);

        return new TokenResponse(
            accessToken, refreshTokenValue, expiresAt, MapToDto(user, roles, permissions));
    }

    /// <summary>
    /// SECURITY: Records a failed login attempt with incrementing counter.
    /// </summary>
    private async Task RecordFailedLoginAsync(User? user, LoginRequest request)
    {
        if (user is null) return;

        user.FailedLoginAttempts++;
        await _userManager.UpdateAsync(default);

        await LogSecurityEventAsync(user.Id, user.UserName!, "login_failed",
            "warning", request.IpAddress, request.UserAgent, request.DeviceInfo,
            $"Attempt {user.FailedLoginAttempts}/{MaxFailedAttempts}");
    }

    public async Task<TokenResponse> RegisterAsync(RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var username = string.IsNullOrWhiteSpace(request.Username)
            ? request.Email?.Split('@')[0] ?? throw new InvalidOperationException("Email is required to derive a username.")
            : request.Username;

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new InvalidOperationException("Email is required.");

        var existingUser = await _userManager.FindByNameAsync(username);
        if (existingUser is not null)
            throw new InvalidOperationException("Username already exists.");

        var existingEmail = await _userManager.FindByEmailAsync(request.Email);
        if (existingEmail is not null)
            throw new InvalidOperationException("Email already registered.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = username,
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

        return new TokenResponse(accessToken, refreshTokenValue, expiresAt, MapToDto(user, roles, permissions));
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

        return new TokenResponse(accessToken, newRefreshTokenValue, expiresAt, MapToDto(user, roles, permissions));
    }

    public async Task<UserDto> GetUserByIdAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            throw new KeyNotFoundException("User not found.");

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsForRolesAsync(roles, cancellationToken);
        return MapToDto(user, roles, permissions);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        await _refreshTokenStore.RevokeAsync(refreshToken, cancellationToken);
        _logger.LogInformation("User logout - refresh token revoked");
    }

    private static UserDto MapToDto(User user, IList<string> roles, IList<string>? permissions = null) => new(
        user.Id, user.UserName!, user.Email!,
        user.FirstName, user.LastName, user.MiddleName,
        user.FullName, user.LicenseNumber, user.Specialty, roles, permissions);

    // ─── Security Event Logging ─────────────────────────────────────

    /// <summary>
    /// SECURITY: Logs a structured security event to the database.
    /// These events power audit trails, threat detection, and login notifications.
    /// </summary>
    private async Task LogSecurityEventAsync(
        Guid? userId, string? userName, string eventType, string? severity,
        string? ipAddress, string? userAgent, string? deviceInfo, string? details)
    {
        try
        {
            _context.SecurityEvents.Add(new SecurityEvent
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                UserName = userName,
                EventType = eventType,
                Severity = severity ?? "info",
                IpAddress = ipAddress,
                UserAgent = userAgent,
                DeviceInfo = deviceInfo,
                Details = details,
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(default);
        }
        catch (Exception ex)
        {
            // SECURITY: Never let event logging block the auth flow
            _logger.LogError(ex, "Failed to log security event: {EventType}", eventType);
        }
    }

}
