using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Authorization;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.SharedKernel.Authorization;
using His.Hope.SharedKernel.Domain.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Infrastructure.Services;

public partial class IdentityService : IIdentityService
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
    private async Task<List<string>> GetPermissionsForRolesAsync(IEnumerable<string> roleNames, Guid? userId = null, CancellationToken ct = default)
    {
        var roleIds = await _context.Roles
            .Where(r => roleNames.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync(ct);

        var permissions = await _context.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.PermissionCode)
            .Distinct()
            .ToListAsync(ct);

        if (userId is Guid subjectUserId)
        {
            var now = DateTime.UtcNow;

            // IAM control-plane assignments are the authoritative extension to
            // legacy ASP.NET Identity roles. Only active, published sets are
            // projected into a human token, and group assignments are resolved
            // through server-side membership (never from frontend claims).
            var groupIds = await _context.IamGroupMemberships
                .Where(membership => membership.UserId == subjectUserId)
                .Join(_context.IamGroups.Where(group => group.IsActive), membership => membership.GroupId, group => group.Id, (_, group) => group.Id)
                .ToListAsync(ct);
            var assignedSetJson = await _context.IamPermissionSetAssignments
                .Where(assignment => assignment.Status == "active" &&
                    (assignment.ExpiresAt == null || assignment.ExpiresAt > now) &&
                    ((assignment.PrincipalType == "human" && assignment.PrincipalId == subjectUserId) ||
                     (assignment.PrincipalType == "group" && groupIds.Contains(assignment.PrincipalId))))
                .Join(_context.IamPermissionSets.Where(set => set.LifecycleStatus == "published"),
                    assignment => assignment.PermissionSetId,
                    set => set.Id,
                    (_, set) => set.PermissionsJson)
                .ToListAsync(ct);
            var registeredPrefixes = await _context.IamServiceDefinitions
                .Select(service => service.PermissionPrefix)
                .ToArrayAsync(ct);
            foreach (var permissionsJson in assignedSetJson)
            {
                try
                {
                    var assignedPermissions = JsonSerializer.Deserialize<string[]>(permissionsJson) ?? [];
                    permissions.AddRange(assignedPermissions.Where(permission =>
                        PermissionCatalogRules.IsValid(permission, registeredPrefixes) &&
                        !permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)));
                }
                catch (JsonException)
                {
                    LogMalformedPermissionSet(_logger, subjectUserId);
                }
            }

            var breakGlassPermissions = await _context.BreakGlassRequests
                .Where(request => request.SubjectUserId == subjectUserId && request.Status == "approved" && request.RevokedAt == null && request.ExpiresAt > now)
                .Select(request => request.PermissionCode)
                .Distinct()
                .ToListAsync(ct);
            permissions.AddRange(breakGlassPermissions.Where(permission => !permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)));

            // A human permission boundary is a hard upper envelope. If one is
            // present, every role, group and break-glass grant must fit inside
            // its allow-list before the token is minted.
            var boundaries = await _context.IamPermissionBoundaries
                .Where(boundary => boundary.IsActive && boundary.PrincipalType == "human" && boundary.PrincipalId == subjectUserId)
                .Select(boundary => boundary.AllowedPermissionsJson)
                .ToListAsync(ct);
            foreach (var allowedJson in boundaries)
            {
                try
                {
                    var allowed = (JsonSerializer.Deserialize<string[]>(allowedJson) ?? [])
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    permissions = permissions
                        .Where(allowed.Contains)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
                catch (JsonException)
                {
                    LogMalformedPermissionBoundary(_logger, subjectUserId);
                    permissions = [];
                }
            }
        }

        return permissions;
    }

    public async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return [];

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsForRolesAsync(roles, userId, cancellationToken);
        var configuredIds = _configuration.GetSection("Identity:SuperAdmin:UserIds").Get<string[]>() ?? [];
        return _configuration.GetValue("Identity:SuperAdmin:RestrictToControlPlane", false) &&
            configuredIds.Any(id => Guid.TryParse(id, out var configuredId) && configuredId == userId)
            ? PrivilegedIdentityPermissionBoundary.Filter(permissions)
            : permissions;
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

            LogLockedAccountAttempt(_logger, user.Id, request.IpAddress, remaining.TotalMinutes);
            throw new UnauthorizedAccessException(
                $"Account temporarily locked. Try again in {remaining.TotalMinutes:F0} minutes.");
        }

        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password ?? string.Empty))
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

                LogAccountLocked(_logger, user.Id, request.IpAddress, LockoutDuration.TotalMinutes);
            }

            LogFailedLogin(_logger, request.Username, request.IpAddress, request.UserAgent);
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            await LogSecurityEventAsync(user.Id, user.UserName!, "deactivated_login_attempt",
                "warning", request.IpAddress, request.UserAgent, request.DeviceInfo,
                "Attempted login on deactivated account");
            LogDeactivatedLogin(_logger, user.Id);
            throw new UnauthorizedAccessException("Account is deactivated.");
        }

        // SECURITY: Check if password change is required
        var passwordMaxAgeDays = _configuration.GetValue("Security:PasswordMaxAgeDays", 90);
        if (user.LastPasswordChangedAt.HasValue &&
            (DateTime.UtcNow - user.LastPasswordChangedAt.Value).TotalDays > passwordMaxAgeDays)
        {
            LogPasswordExpired(_logger, user.Id);
            // Not blocking login — will be handled by client-side force-change
        }

        // SECURITY: Reset lockout counters on successful login
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsForRolesAsync(roles, user.Id, cancellationToken);
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

        LogUserLoggedIn(_logger, user.Id, string.Join(",", roles), permissions.Count, familyId, request.IpAddress);

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
        await _userManager.UpdateAsync(user);

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
            FirstName = request.FirstName ?? string.Empty,
            LastName = request.LastName ?? string.Empty,
            MiddleName = request.MiddleName,
            LicenseNumber = request.LicenseNumber,
            Specialty = request.Specialty,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password ?? string.Empty);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Registration failed: {errors}");
        }

        await _userManager.AddToRoleAsync(user, "Provider");

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsForRolesAsync(roles, user.Id, cancellationToken);
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

        LogUserRegistered(_logger, user.Id, request.Username);

        return new TokenResponse(accessToken, refreshTokenValue, expiresAt, MapToDto(user, roles, permissions));
    }

    public async Task<TokenResponse> RefreshTokenAsync(RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var principal = _tokenGenerator.GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal is null)
            throw new UnauthorizedAccessException("Invalid access token.");

        var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value;
        if (userId is null)
            throw new UnauthorizedAccessException("Invalid access token.");

        // Atomically consume the token so concurrent refresh requests cannot both succeed.
        var (existingRecord, wasReused) = await _refreshTokenStore
            .ConsumeAsync(request.RefreshToken, cancellationToken);
        var familyId = existingRecord?.FamilyId;

        if (wasReused)
        {
            LogRefreshTokenReuse(_logger, userId, familyId);
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

        // Token rotation: the old token was atomically consumed above; issue a new one.
        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsForRolesAsync(roles, user.Id, cancellationToken);
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

        LogTokenRefreshed(_logger, userId, newRecord.FamilyId, newRecord.Generation);

        return new TokenResponse(accessToken, newRefreshTokenValue, expiresAt, MapToDto(user, roles, permissions));
    }

    public async Task<UserDto> GetUserByIdAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = Guard.Against.NotFound(
            await _userManager.Users.AsNoTracking()
            .TagWith("Identity.Users.ServiceGetUserById")
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken), nameof(User), userId);

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsForRolesAsync(roles, user.Id, cancellationToken);
        return MapToDto(user, roles, permissions);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        await _refreshTokenStore.RevokeAsync(refreshToken, cancellationToken);
        LogUserLogout(_logger);
    }

    public async Task<string> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = Guard.Against.NotFound(
            await _userManager.FindByEmailAsync(email), nameof(User), email);

        return await _userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email)
            ?? Guard.Against.NotFound<User>(null, nameof(User), email);

        await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var hasher = new PasswordHasher<User>();
            var priorHash = user.PasswordHash;
            var recentHashes = await _context.UserPasswordHistories.AsNoTracking()
                .TagWith("Identity.Password.ResetRecentHistory")
                .Where(item => item.UserId == user.Id)
                .OrderByDescending(item => item.ChangedAt)
                .Take(5)
                .ToListAsync(cancellationToken);
            foreach (var oldHash in recentHashes)
            {
                var result = hasher.VerifyHashedPassword(user, oldHash.PasswordHash, newPassword);
                if (result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded)
                    throw new InvalidOperationException("Cannot reuse a recent password.");
            }

            var resetResult = await _userManager.ResetPasswordAsync(user, token, newPassword);
            if (!resetResult.Succeeded)
            {
                var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Password reset failed: {errors}");
            }

            user.LastPasswordChangedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(priorHash))
                await RecordPasswordHistoryAsync(user.Id, priorHash, cancellationToken);
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                throw new InvalidOperationException("Unable to persist password policy metadata.");
            await transaction.CommitAsync(cancellationToken);
            LogPasswordReset(_logger, user.Id);
        });
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? Guard.Against.NotFound<User>(null, nameof(User), userId);

        var checkResult = await _userManager.CheckPasswordAsync(user, currentPassword);
        if (!checkResult)
            throw new UnauthorizedAccessException("Current password is incorrect.");

        await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var hasher = new PasswordHasher<User>();
            var priorHash = user.PasswordHash;
            var recentHashes = await _context.UserPasswordHistories.AsNoTracking()
                .TagWith("Identity.Password.ChangeRecentHistory")
                .Where(item => item.UserId == user.Id)
                .OrderByDescending(item => item.ChangedAt)
                .Take(5)
                .ToListAsync(cancellationToken);
            foreach (var oldHash in recentHashes)
            {
                var result = hasher.VerifyHashedPassword(user, oldHash.PasswordHash, newPassword);
                if (result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded)
                    throw new InvalidOperationException("Cannot reuse a recent password.");
            }

            var changeResult = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (!changeResult.Succeeded)
            {
                var errors = string.Join(", ", changeResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Password change failed: {errors}");
            }

            user.LastPasswordChangedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(priorHash))
                await RecordPasswordHistoryAsync(user.Id, priorHash, cancellationToken);
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                throw new InvalidOperationException("Unable to persist password policy metadata.");
            await transaction.CommitAsync(cancellationToken);
            LogPasswordChanged(_logger, user.Id);
        });
    }

    private async Task RecordPasswordHistoryAsync(Guid userId, string passwordHash, CancellationToken cancellationToken)
    {
        _context.UserPasswordHistories.Add(new UserPasswordHistory
        {
            UserId = userId,
            PasswordHash = passwordHash,
            ChangedAt = DateTime.UtcNow
        });

        var stale = await _context.UserPasswordHistories.AsNoTracking()
            .TagWith("Identity.Password.TrimHistory")
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.ChangedAt)
            .Skip(5)
            .ToListAsync(cancellationToken);
        _context.UserPasswordHistories.RemoveRange(stale);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> GenerateEmailConfirmationTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? Guard.Against.NotFound<User>(null, nameof(User), userId);

        return await _userManager.GenerateEmailConfirmationTokenAsync(user);
    }

    public async Task ConfirmEmailAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email)
            ?? Guard.Against.NotFound<User>(null, nameof(User), email);

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Email confirmation failed: {errors}");
        }

        LogEmailConfirmed(_logger, user.Id);
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
            _context.SecuritySignalOutbox.Add(new SecuritySignalOutbox
            {
                EventType = eventType,
                Subject = userId?.ToString() ?? userName ?? "unknown",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    eventType,
                    subject = userId?.ToString() ?? "unknown",
                    severity = severity ?? "info",
                    occurredAt = DateTime.UtcNow
                })
            });
            await _context.SaveChangesAsync(default);
        }
        catch (Exception ex)
        {
            // SECURITY: Never let event logging block the auth flow
            LogSecurityEventFailed(_logger, ex, eventType);
        }
    }

    [LoggerMessage(EventId = 4301, Level = LogLevel.Warning, Message = "Malformed IAM permission set for subject {SubjectUserId}.")]
    private static partial void LogMalformedPermissionSet(ILogger logger, Guid subjectUserId);

    [LoggerMessage(EventId = 4302, Level = LogLevel.Warning, Message = "Malformed IAM permission boundary for subject {SubjectUserId}.")]
    private static partial void LogMalformedPermissionBoundary(ILogger logger, Guid subjectUserId);

    [LoggerMessage(EventId = 4303, Level = LogLevel.Warning, Message = "Locked account login attempt for user {UserId} from {IpAddress}; remaining lockout minutes {RemainingMinutes}.")]
    private static partial void LogLockedAccountAttempt(ILogger logger, Guid userId, string? ipAddress, double remainingMinutes);

    [LoggerMessage(EventId = 4304, Level = LogLevel.Warning, Message = "Account {UserId} locked after repeated failed login attempts from {IpAddress}; lockout minutes {LockoutMinutes}.")]
    private static partial void LogAccountLocked(ILogger logger, Guid userId, string? ipAddress, double lockoutMinutes);

    [LoggerMessage(EventId = 4305, Level = LogLevel.Warning, Message = "Failed login for {Username} from {IpAddress} using {UserAgent}.")]
    private static partial void LogFailedLogin(ILogger logger, string? username, string? ipAddress, string? userAgent);

    [LoggerMessage(EventId = 4306, Level = LogLevel.Warning, Message = "Deactivated login attempt for user {UserId}.")]
    private static partial void LogDeactivatedLogin(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 4307, Level = LogLevel.Warning, Message = "Password expired for user {UserId}.")]
    private static partial void LogPasswordExpired(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 4308, Level = LogLevel.Information, Message = "User {UserId} logged in with roles {Roles}, permissions {PermissionCount}, family {FamilyId} from {IpAddress}.")]
    private static partial void LogUserLoggedIn(ILogger logger, Guid userId, string roles, int permissionCount, string familyId, string? ipAddress);

    [LoggerMessage(EventId = 4309, Level = LogLevel.Information, Message = "User {UserId} registered with username {Username}.")]
    private static partial void LogUserRegistered(ILogger logger, Guid userId, string? username);

    [LoggerMessage(EventId = 4310, Level = LogLevel.Critical, Message = "Refresh token reuse detected for user {UserId}, family {FamilyId}.")]
    private static partial void LogRefreshTokenReuse(ILogger logger, string userId, string? familyId);

    [LoggerMessage(EventId = 4311, Level = LogLevel.Debug, Message = "Refresh token issued for user {UserId}, family {FamilyId}, generation {Generation}.")]
    private static partial void LogTokenRefreshed(ILogger logger, string userId, string familyId, int generation);

    [LoggerMessage(EventId = 4312, Level = LogLevel.Information, Message = "User logged out.")]
    private static partial void LogUserLogout(ILogger logger);

    [LoggerMessage(EventId = 4313, Level = LogLevel.Information, Message = "Password reset completed for user {UserId}.")]
    private static partial void LogPasswordReset(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 4314, Level = LogLevel.Information, Message = "Password changed for user {UserId}.")]
    private static partial void LogPasswordChanged(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 4315, Level = LogLevel.Information, Message = "Email confirmed for user {UserId}.")]
    private static partial void LogEmailConfirmed(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 4316, Level = LogLevel.Error, Message = "Security event logging failed for event type {EventType}.")]
    private static partial void LogSecurityEventFailed(ILogger logger, Exception exception, string eventType);

}
