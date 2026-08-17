using Grpc.Core;
using His.Hope.Identity.Grpc;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.SharedKernel.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace His.Hope.IdentityService.Api.Services;

public class GrpcIdentityService : global::His.Hope.Identity.Grpc.IdentityService.IdentityServiceBase
{
    private readonly IdentityDbContext _db;
    private readonly UserManager<User> _userManager;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<GrpcIdentityService> _logger;

    public GrpcIdentityService(
        IdentityDbContext db,
        UserManager<User> userManager,
        IConnectionMultiplexer redis,
        ILogger<GrpcIdentityService> logger)
    {
        _db = db;
        _userManager = userManager;
        _redis = redis;
        _logger = logger;
    }

    public override async Task<IntrospectResponse> IntrospectToken(
        IntrospectRequest request, ServerCallContext context)
    {
        var token = request.Token;
        if (string.IsNullOrEmpty(token))
            return new IntrospectResponse { Active = false };

        var claims = DecodeJwtClaims(token);
        if (claims is null || claims.Count == 0)
            return new IntrospectResponse { Active = false };

        var jti = claims.GetValueOrDefault("jti", "");
        var sub = claims.GetValueOrDefault("sub", "");

        // Check blacklist
        if (!string.IsNullOrEmpty(jti))
        {
            var db = _redis.GetDatabase();
            var isBlacklisted = await db.KeyExistsAsync($"token_blacklist:{jti}");
            if (isBlacklisted)
                return new IntrospectResponse { Active = false };
        }

        // Resolve permissions from roles
        var permissions = new List<string>();
        var roles = new List<string>();

        if (!string.IsNullOrEmpty(sub) && Guid.TryParse(sub, out var userId))
        {
            var user = await _userManager.FindByIdAsync(sub);
            if (user is not null)
            {
                roles = (await _userManager.GetRolesAsync(user)).ToList();
                permissions = await GetUserPermissionsAsync(userId);
            }
        }

        return new IntrospectResponse
        {
            Active = true,
            Sub = sub ?? "",
            ClientId = claims.GetValueOrDefault("client_id", ""),
            Exp = long.TryParse(claims.GetValueOrDefault("exp", "0"), out var exp) ? exp : 0,
            Iat = long.TryParse(claims.GetValueOrDefault("iat", "0"), out var iat) ? iat : 0,
            Scope = claims.GetValueOrDefault("scope", ""),
            Permissions = { permissions },
            Roles = { roles },
            Username = claims.GetValueOrDefault("unique_name", ""),
            FullName = claims.GetValueOrDefault("fullName", ""),
            LicenseNumber = claims.GetValueOrDefault("licenseNumber", ""),
            FacilityId = claims.GetValueOrDefault("facilityId", ""),
            Amr = { claims.GetValueOrDefault("amr", "pwd") },
            Jti = jti
        };
    }

    public override async Task<GetUserResponse> GetUser(
        GetUserRequest request, ServerCallContext context)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user is null)
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await GetUserPermissionsAsync(user.Id);

        return new GetUserResponse
        {
            UserId = user.Id.ToString(),
            Username = user.UserName ?? "",
            Email = user.Email ?? "",
            FullName = user.FullName ?? "",
            IsActive = user.IsActive,
            MfaEnabled = user.TwoFactorEnabled,
            Roles = { roles },
            Permissions = { permissions },
            FacilityId = ""
        };
    }

    public override async Task<CheckPermissionResponse> CheckPermission(
        CheckPermissionRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return new CheckPermissionResponse { HasPermission = false };

        var permissions = await GetUserPermissionsAsync(userId);
        var hasPermission = permissions.Contains(request.PermissionCode, StringComparer.OrdinalIgnoreCase);

        return new CheckPermissionResponse { HasPermission = hasPermission };
    }

    public override async Task<CheckAnyPermissionResponse> CheckAnyPermission(
        CheckAnyPermissionRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return new CheckAnyPermissionResponse { HasAny = false };

        var permissions = await GetUserPermissionsAsync(userId);
        var hasAny = request.PermissionCodes.Any(pc =>
            permissions.Contains(pc, StringComparer.OrdinalIgnoreCase));

        return new CheckAnyPermissionResponse { HasAny = hasAny };
    }

    public override async Task<GetUserRolesResponse> GetUserRoles(
        GetUserRolesRequest request, ServerCallContext context)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user is null) return new GetUserRolesResponse();

        var roles = await _userManager.GetRolesAsync(user);
        return new GetUserRolesResponse { Roles = { roles } };
    }

    public override async Task<RevokeUserTokensResponse> RevokeUserTokens(
        RevokeUserTokensRequest request, ServerCallContext context)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user is null) return new RevokeUserTokensResponse { TokensRevoked = 0 };

        await _userManager.UpdateSecurityStampAsync(user);

        _logger.LogInformation("User tokens revoked: userId={UserId}, reason={Reason}",
            request.UserId, request.Reason);

        return new RevokeUserTokensResponse { TokensRevoked = 1 };
    }

    private async Task<List<string>> GetUserPermissionsAsync(Guid userId)
    {
        var permissions = await _db.RolePermissions
            .Where(rp => _db.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.RoleId)
                .Contains(rp.RoleId))
            .Select(rp => rp.PermissionCode)
            .Distinct()
            .ToListAsync();

        var now = DateTime.UtcNow;
        var groupIds = await _db.IamGroupMemberships
            .Where(membership => membership.UserId == userId)
            .Select(membership => membership.GroupId)
            .ToListAsync();
        var assignedSetJson = await _db.IamPermissionSetAssignments
            .Where(assignment => assignment.Status == "active" &&
                (assignment.ExpiresAt == null || assignment.ExpiresAt > now) &&
                ((assignment.PrincipalType == "human" && assignment.PrincipalId == userId) ||
                 (assignment.PrincipalType == "group" && groupIds.Contains(assignment.PrincipalId))))
            .Join(_db.IamPermissionSets.Where(set => set.LifecycleStatus == "published"),
                assignment => assignment.PermissionSetId,
                set => set.Id,
                (_, set) => set.PermissionsJson)
            .ToListAsync();
        foreach (var permissionsJson in assignedSetJson)
        {
            try
            {
                var assigned = JsonSerializer.Deserialize<string[]>(permissionsJson) ?? [];
                permissions.AddRange(assigned.Where(HisHopePermissions.IsValid));
            }
            catch (JsonException)
            {
                _logger.LogWarning("Ignoring malformed IAM permission set JSON for user {UserId}.", userId);
            }
        }

        permissions.AddRange(await _db.BreakGlassRequests
            .Where(request => request.SubjectUserId == userId && request.Status == "approved" && request.RevokedAt == null && request.ExpiresAt > now)
            .Select(request => request.PermissionCode)
            .ToListAsync());

        var boundaries = await _db.IamPermissionBoundaries
            .Where(boundary => boundary.IsActive && boundary.PrincipalType == "human" && boundary.PrincipalId == userId)
            .Select(boundary => boundary.AllowedPermissionsJson)
            .ToListAsync();
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
                _logger.LogError("Denying permissions because boundary JSON is malformed for user {UserId}.", userId);
                permissions = [];
            }
        }
        return permissions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static Dictionary<string, string> DecodeJwtClaims(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return new Dictionary<string, string>();
            var payload = parts[1];
            var base64 = payload.Replace('-', '+').Replace('_', '/');
            var padded = base64.PadRight(((base64.Length + 3) / 4) * 4, '=');
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}
