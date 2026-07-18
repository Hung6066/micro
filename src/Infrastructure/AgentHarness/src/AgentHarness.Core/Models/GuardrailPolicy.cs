namespace His.Hope.AgentHarness.Core.Models;

/// <summary>
/// Defines a guardrail policy for a specific action type.
/// Policies can block, require approval, or allow actions.
/// </summary>
public class GuardrailPolicy
{
    public string ActionType { get; set; } = string.Empty;
    public string AgentPattern { get; set; } = "*";
    public GuardrailAction Action { get; set; } = GuardrailAction.Allow;
    public string? Reason { get; set; }
}

public enum GuardrailAction { Allow, Block, RequireApproval }

/// <summary>
/// A pending human-in-the-loop approval request.
/// Created when a guardrail requires approval for a sensitive action.
/// </summary>
public class PendingApproval
{
    public Guid Id { get; private set; }
    public string ActionType { get; private set; } = string.Empty;
    public string RequestedBy { get; private set; } = string.Empty;
    public string Details { get; private set; } = string.Empty;
    public string Status { get; private set; } = "pending";
    public string? ApprovedBy { get; private set; }
    public string? RejectReason { get; private set; }
    public string? ContextJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    private PendingApproval() { }

    public static PendingApproval Create(string actionType, string requestedBy, string details, string? contextJson = null)
    {
        return new PendingApproval
        {
            Id = Guid.NewGuid(),
            ActionType = actionType,
            RequestedBy = requestedBy,
            Details = details,
            Status = "pending",
            ContextJson = contextJson,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Approve(string approvedBy)
    {
        Status = "approved";
        ApprovedBy = approvedBy;
        ResolvedAt = DateTime.UtcNow;
    }

    public void Reject(string rejectedBy, string reason)
    {
        Status = "rejected";
        ApprovedBy = rejectedBy;
        RejectReason = reason;
        ResolvedAt = DateTime.UtcNow;
    }
}
