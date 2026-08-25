using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace His.Hope.Authorization;

/// <summary>
/// Local PEP/PDP baseline for resource-aware checks. Domain services remain
/// responsible for loading resource metadata and applying query filters.
/// </summary>
public sealed class AuthorizationEvaluator(
    IAuthorizationDecisionSink? decisionSink = null,
    IHttpContextAccessor? httpContextAccessor = null,
    IAuthorizationShadowProbe? shadowProbe = null,
    ICrossTenantAccessPolicy? crossTenantPolicy = null) : IResourceAuthorizationEvaluator
{
    public async ValueTask<AuthorizationDecision> EvaluateAsync(
        AuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        var resourceType = context.Resource?.Type;
        AuthorizationDecision decision;

        if (context.Principal.Identity?.IsAuthenticated != true)
        {
            decision = AuthorizationDecision.Deny(context.Action, "unauthenticated", resourceType);
        }
        else if (string.IsNullOrWhiteSpace(context.Action))
        {
            decision = AuthorizationDecision.Deny("unknown", "invalid_action", resourceType);
        }
        else if (context.RequireResource && context.Resource is null)
        {
            decision = AuthorizationDecision.Deny(context.Action, "resource_required", resourceType);
        }
        else if (context.ResourceLookupFailed)
        {
            decision = AuthorizationDecision.Deny(context.Action, "resource_not_found", resourceType);
        }
        else if (!HasPermission(context.Principal, context.Action))
        {
            decision = AuthorizationDecision.Deny(context.Action, "permission_missing", resourceType);
        }
        else if (context.Resource is not null && !HasFacilityAccess(context.Principal, context.Resource.FacilityId))
        {
            decision = AuthorizationDecision.Deny(context.Action, "facility_scope_denied", resourceType);
        }
        else if (context.Resource is not null &&
                 TenantAccessEvaluator.Evaluate(context.Principal, context.Resource, context.Action, crossTenantPolicy) is { } tenantReason)
        {
            decision = AuthorizationDecision.Deny(context.Action, tenantReason, resourceType);
        }
        else if (context.Resource is not null &&
                 AuthorizationConstraintEvaluator.Evaluate(context.Principal, context.Resource) is { } constraintReason)
        {
            decision = AuthorizationDecision.Deny(context.Action, constraintReason, resourceType);
        }
        else if (context.Resource is not null &&
                 ResourcePolicyEvaluator.Evaluate(context.Principal, context.Resource, context.Action) is { } policyReason)
        {
            decision = AuthorizationDecision.Deny(context.Action, policyReason, resourceType);
        }
        else
        {
            decision = AuthorizationDecision.Allow(context.Action, resourceType);
        }

        // Shadow/canary telemetry is strictly advisory and cannot affect the
        // local fail-closed decision path.
        if (shadowProbe is not null)
        {
            try
            {
                await shadowProbe.ObserveAsync(context, decision, cancellationToken);
            }
            catch when (!cancellationToken.IsCancellationRequested)
            {
                // Shadow/PDP telemetry is never an authorization dependency.
                // Keep the local decision (including deny) when the probe fails.
            }
        }

        if (decisionSink is not null)
        {
            var httpContext = httpContextAccessor?.HttpContext;
            await decisionSink.WriteAsync(
                new AuthorizationDecisionAudit(
                    decision,
                    SubjectId(context.Principal),
                    context.Resource?.TenantId,
                    context.Resource?.FacilityId,
                    httpContext?.TraceIdentifier),
                cancellationToken);
        }

        return decision;
    }

    private static bool HasPermission(ClaimsPrincipal principal, string action) =>
        principal.FindAll("permissions")
            .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Any(permission => string.Equals(permission, action, StringComparison.OrdinalIgnoreCase));

    private static bool HasFacilityAccess(ClaimsPrincipal principal, string? facilityId)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            return true;

        return FacilityAccessScope.FromPrincipal(principal).CanAccess(facilityId);
    }

    private static string? SubjectId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue("sub");
}

public sealed class NullAuthorizationDecisionSink : IAuthorizationDecisionSink
{
    public ValueTask WriteAsync(AuthorizationDecisionAudit audit, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}

/// <summary>
/// Default audit adapter. It records decision metadata only; canonical
/// resource ids and policy internals are intentionally excluded from logs.
/// </summary>
public sealed class LoggingAuthorizationDecisionSink(
    ILogger<LoggingAuthorizationDecisionSink> logger) : IAuthorizationDecisionSink
{
    public ValueTask WriteAsync(
        AuthorizationDecisionAudit audit,
        CancellationToken cancellationToken = default)
    {
        var logLevel = audit.Decision.Allowed ? LogLevel.Information : LogLevel.Warning;
        logger.Log(logLevel,
            "Authorization decision {DecisionId}: {Status} action={Action} reason={ReasonCode} resourceType={ResourceType} subject={SubjectId} tenant={TenantId} facility={FacilityId} correlation={CorrelationId}",
            audit.Decision.DecisionId,
            audit.Decision.Status,
            audit.Decision.Action,
            audit.Decision.ReasonCode,
            audit.Decision.ResourceType,
            audit.SubjectId,
            audit.TenantId,
            audit.FacilityId,
            audit.CorrelationId);
        return ValueTask.CompletedTask;
    }
}
