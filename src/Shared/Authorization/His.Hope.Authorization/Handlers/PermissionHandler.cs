using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using His.Hope.Authorization.Requirements;
using His.Hope.SharedKernel.Protocol;

namespace His.Hope.Authorization.Handlers;

public sealed class PermissionHandler(
    ILogger<PermissionHandler> logger,
    IAuthorizationDecisionSink? decisionSink = null,
    OpenFgaCanaryAuthorizer? canaryAuthorizer = null) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await RecordDecisionAsync(context, requirement, false, "unauthenticated");
            return;
        }

        var permissions = context.User.FindAll(HisHopeProtocolConstants.Claims.Permissions)
            .SelectMany(c => c.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (permissions.Count > 0)
        {
            var allowed = permissions.Contains(requirement.PermissionCode);
            if (allowed && canaryAuthorizer is not null &&
                !await canaryAuthorizer.AllowsAsync(context.User, requirement.PermissionCode))
            {
                allowed = false;
            }
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
            context.User.FindFirst(HisHopeProtocolConstants.Claims.Subject)?.Value,
            context.User.FindFirst(HisHopeProtocolConstants.Claims.Tenant)?.Value,
            context.User.FindFirst(HisHopeProtocolConstants.Claims.FacilityId)?.Value,
            null));
    }
}
