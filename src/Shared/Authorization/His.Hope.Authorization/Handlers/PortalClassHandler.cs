using His.Hope.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace His.Hope.Authorization.Handlers;

public sealed class PortalClassHandler : AuthorizationHandler<PortalClassRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PortalClassRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        var portalClass = context.User.FindFirst(PortalClassConstants.Claim)?.Value;
        if (string.IsNullOrWhiteSpace(portalClass))
            return Task.CompletedTask;

        if (requirement.AllowedPortalClasses.Any(allowed =>
                string.Equals(portalClass, allowed, StringComparison.OrdinalIgnoreCase)))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
