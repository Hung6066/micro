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

        // Runtime authorization is driven only by issued permission claims. Role
        // mappings are used when minting tokens, but must not become a second
        // authorization source of truth when a token is missing its permissions.
        logger.LogWarning("Permission denied: token has no permissions claim for {Permission}", requirement.PermissionCode);
        return Task.CompletedTask;
    }
}
