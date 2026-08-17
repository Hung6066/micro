using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using His.Hope.Authorization.Requirements;

namespace His.Hope.Authorization.Handlers;

public sealed class PermissionHandler(
    ILogger<PermissionHandler> logger,
    IAuthorizationDecisionSink? decisionSink = null) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await RecordDecisionAsync(context, requirement, false, "unauthenticated");
            return;
        }

        var permissions = context.User.FindAll("permissions")
            .SelectMany(c => c.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (permissions.Count > 0)
        {
            var allowed = permissions.Contains(requirement.PermissionCode);
            if (allowed) context.Succeed(requirement);
            else logger.LogDebug("Permission denied for {Permission}", requirement.PermissionCode);
            await RecordDecisionAsync(context, requirement, allowed, allowed ? "allowed" : "permission_missing");
            return;
        }

        // Runtime authorization is driven only by issued permission claims. Role
        // mappings are used when minting tokens, but must not become a second
        // authorization source of truth when a token is missing its permissions.
        logger.LogWarning("Permission denied: token has no permissions claim for {Permission}", requirement.PermissionCode);
        await RecordDecisionAsync(context, requirement, false, "permission_missing");
    }

    private async Task RecordDecisionAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement,
        bool allowed,
        string reasonCode)
    {
        if (decisionSink is null)
            return;

        await decisionSink.WriteAsync(new AuthorizationDecisionAudit(
            allowed
                ? AuthorizationDecision.Allow(requirement.PermissionCode)
                : AuthorizationDecision.Deny(requirement.PermissionCode, reasonCode),
            context.User.FindFirst("sub")?.Value,
            context.User.FindFirst("tenant")?.Value,
            context.User.FindFirst("facility_id")?.Value,
            null));
    }
}
