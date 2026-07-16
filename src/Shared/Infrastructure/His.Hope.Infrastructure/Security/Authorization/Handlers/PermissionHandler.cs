using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using His.Hope.Infrastructure.Security.Authorization.Requirements;

namespace His.Hope.Infrastructure.Security.Authorization.Handlers;

/// <summary>
/// Authorization handler that evaluates <see cref="PermissionRequirement"/>.
/// Checks the current user's JWT claims for a "permissions" claim containing
/// the required permission code.
///
/// Supports two modes:
/// 1. Direct claim check: Looks for "permissions" claim with matching value
/// 2. Role fallback: If "permissions" claim is absent, falls back to the "role"
///    claim and checks against a configurable role-to-permission mapping
/// </summary>
public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ILogger<PermissionHandler> _logger;

    public PermissionHandler(ILogger<PermissionHandler> logger)
    {
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // SECURITY: If user is not authenticated, deny immediately
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning(
                "Permission check failed: user not authenticated for permission {Permission}",
                requirement.PermissionCode);
            return Task.CompletedTask;
        }

        // PRIORITY 1: Check direct "permissions" claims from JWT
        var permissionsClaims = context.User.FindAll("permissions")
            .SelectMany(c => c.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (permissionsClaims.Count > 0)
        {
            if (permissionsClaims.Contains(requirement.PermissionCode))
            {
                _logger.LogDebug(
                    "Permission granted: user has direct permission claim {Permission}",
                    requirement.PermissionCode);
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // User has permission claims but not this specific one - deny
            _logger.LogWarning(
                "Permission denied: user lacks permission {Permission}",
                requirement.PermissionCode);
            return Task.CompletedTask;
        }

        // PRIORITY 2: Fallback to role-based permission mapping
        var roleClaims = context.User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (roleClaims.Count > 0)
        {
            var rolePermissions = RolePermissionMapping.GetPermissionsForRoles(roleClaims);
            if (rolePermissions.Contains(requirement.PermissionCode))
            {
                _logger.LogDebug(
                    "Permission granted via role fallback: role {Roles} has permission {Permission}",
                    string.Join(", ", roleClaims),
                    requirement.PermissionCode);
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }

        _logger.LogWarning(
            "Permission denied: no matching claim or role mapping for {Permission}",
            requirement.PermissionCode);

        return Task.CompletedTask;
    }
}
