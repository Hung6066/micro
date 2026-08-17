using Microsoft.AspNetCore.Authorization;
using His.Hope.Authorization.Requirements;

namespace His.Hope.Authorization.Handlers;

public sealed class ScopeHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ScopeRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        var scopes = context.User.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(
                ' ',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Concat(context.User.FindAll("scp")
                .SelectMany(claim => claim.Value.Split(
                    ' ',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)))
            .ToHashSet(StringComparer.Ordinal);

        if (requirement.Scopes.Any(scopes.Contains))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
