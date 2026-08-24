using System.Security.Claims;
using His.Hope.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace His.Hope.Authorization.Handlers;

public sealed class CommerceScopeOrPermissionHandler
    : AuthorizationHandler<CommerceScopeOrPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CommerceScopeOrPermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        if (HasScope(context.User, requirement.Scope) ||
            HasPermission(context.User, requirement.PermissionCode))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }

    private static bool HasScope(ClaimsPrincipal user, string scope)
    {
        var scopes = user.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(
                ' ',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Concat(user.FindAll("scp")
                .SelectMany(claim => claim.Value.Split(
                    ' ',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)))
            .ToHashSet(StringComparer.Ordinal);

        return scopes.Contains(scope);
    }

    private static bool HasPermission(ClaimsPrincipal user, string permissionCode) =>
        user.FindAll("permissions")
            .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Any(value => string.Equals(value, permissionCode, StringComparison.OrdinalIgnoreCase));
}
