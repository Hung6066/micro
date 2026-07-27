using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using His.Hope.Authorization.Requirements;

namespace His.Hope.Authorization.Handlers;

public sealed class PermissionHandler(ILogger<PermissionHandler> logger) : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true) return Task.CompletedTask;

        var permissions = context.User.FindAll("permissions")
            .SelectMany(c => c.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (permissions.Count > 0)
        {
            if (permissions.Contains(requirement.PermissionCode)) context.Succeed(requirement);
            else logger.LogDebug("Permission denied for {Permission}", requirement.PermissionCode);
            return Task.CompletedTask;
        }

        var roles = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value);
        if (RolePermissionMapping.GetPermissionsForRoles(roles).Contains(requirement.PermissionCode)) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
