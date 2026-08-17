namespace His.Hope.Authorization;

public enum AuthorizationDecisionStatus
{
    Allow = 0,
    Deny = 1
}

/// <summary>
/// Decision returned by the PEP/PDP seam. Reason codes are deliberately
/// stable and coarse so callers cannot infer policy internals or resource data.
/// </summary>
public sealed record AuthorizationDecision(
    AuthorizationDecisionStatus Status,
    Guid DecisionId,
    string Action,
    string ReasonCode,
    string? ResourceType = null)
{
    public bool Allowed => Status == AuthorizationDecisionStatus.Allow;

    public static AuthorizationDecision Allow(string action, string? resourceType = null) =>
        new(AuthorizationDecisionStatus.Allow, Guid.NewGuid(), action, "allowed", resourceType);

    public static AuthorizationDecision Deny(string action, string reasonCode, string? resourceType = null) =>
        new(AuthorizationDecisionStatus.Deny, Guid.NewGuid(), action, reasonCode, resourceType);
}

public sealed record AuthorizationDecisionAudit(
    AuthorizationDecision Decision,
    string? SubjectId,
    string? TenantId,
    string? FacilityId,
    string? CorrelationId);
