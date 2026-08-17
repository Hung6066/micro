using Microsoft.AspNetCore.Authorization;
using His.Hope.SharedKernel.Authorization;
using His.Hope.Authorization.Requirements;

namespace His.Hope.Authorization.Handlers;

public sealed class PrincipalTypeHandler : AuthorizationHandler<PrincipalTypeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PrincipalTypeRequirement requirement)
    {
        var principalType = context.User.FindFirst(AuthorizationConstants.Claims.PrincipalType)?.Value;
        if (context.User.Identity?.IsAuthenticated == true &&
            principalType is not null && requirement.PrincipalTypes.Contains(principalType))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
