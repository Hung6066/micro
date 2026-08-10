using System.Text.Json;
using Serilog;
using His.Hope.AgentHarness.Application.Services;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Mcp.Tools;

/// <summary>
/// Requests human approval for a guarded action.
/// Creates a PendingApproval record for human review.
/// </summary>
public class RequestApprovalTool
{
    private readonly IStateStore _store;
    private readonly GuardrailService _guardrails;

    public RequestApprovalTool(IStateStore store, GuardrailService guardrails)
    {
        _store = store;
        _guardrails = guardrails;
    }

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var actionType = parameters.GetValueOrDefault("action_type")?.ToString()
            ?? throw new ArgumentException("'action_type' is required.");
        var requestedBy = parameters.GetValueOrDefault("requested_by")?.ToString() ?? "system";
        var details = parameters.GetValueOrDefault("details")?.ToString() ?? actionType;

        var guardrail = _guardrails.Validate(actionType, requestedBy, details);
        if (guardrail.IsAllowed)
        {
            return JsonSerializer.Serialize(new { approved = true, message = "Action allowed by policy" });
        }

        if (guardrail.IsBlocked)
        {
            return JsonSerializer.Serialize(new { approved = false, blocked = true, message = guardrail.Reason });
        }

        var approval = PendingApproval.Create(actionType, requestedBy, details);
        await _store.SavePendingApprovalAsync(approval);

        Log.Information("Approval requested: {Action} by {Requestor} (id={Id})",
            actionType, requestedBy, approval.Id);

        return JsonSerializer.Serialize(new
        {
            approved = false,
            pending = true,
            pending_approval_id = approval.Id.ToString(),
            message = guardrail.Reason
        });
    }
}

/// <summary>
/// Approves a pending action. Called by a human reviewer.
/// </summary>
public class ApproveActionTool
{
    private readonly IStateStore _store;

    public ApproveActionTool(IStateStore store) => _store = store;

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var idStr = parameters.GetValueOrDefault("pending_approval_id")?.ToString()
            ?? throw new ArgumentException("'pending_approval_id' is required.");
        var approvedBy = parameters.GetValueOrDefault("approved_by")?.ToString() ?? "human";

        if (!Guid.TryParse(idStr, out var id))
            throw new ArgumentException("'pending_approval_id' must be a valid GUID.");

        var approval = await _store.GetPendingApprovalAsync(id);
        if (approval == null)
            throw new InvalidOperationException($"Pending approval {id} not found.");

        if (approval.Status != "pending")
            return JsonSerializer.Serialize(new { approved = false, status = approval.Status, message = $"Already {approval.Status}" });

        approval.Approve(approvedBy);
        await _store.SavePendingApprovalAsync(approval);

        Log.Information("Approval granted: {Action} by {ApprovedBy}", approval.ActionType, approvedBy);

        return JsonSerializer.Serialize(new
        {
            approved = true,
            pending_approval_id = idStr,
            action_type = approval.ActionType,
            approved_by = approvedBy
        });
    }
}

/// <summary>
/// Rejects a pending action. Called by a human reviewer.
/// </summary>
public class RejectActionTool
{
    private readonly IStateStore _store;

    public RejectActionTool(IStateStore store) => _store = store;

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var idStr = parameters.GetValueOrDefault("pending_approval_id")?.ToString()
            ?? throw new ArgumentException("'pending_approval_id' is required.");
        var rejectedBy = parameters.GetValueOrDefault("rejected_by")?.ToString() ?? "human";
        var reason = parameters.GetValueOrDefault("reason")?.ToString() ?? "Rejected by human reviewer";

        if (!Guid.TryParse(idStr, out var id))
            throw new ArgumentException("'pending_approval_id' must be a valid GUID.");

        var approval = await _store.GetPendingApprovalAsync(id);
        if (approval == null)
            throw new InvalidOperationException($"Pending approval {id} not found.");

        if (approval.Status != "pending")
            return JsonSerializer.Serialize(new { rejected = false, status = approval.Status, message = $"Already {approval.Status}" });

        approval.Reject(rejectedBy, reason);
        await _store.SavePendingApprovalAsync(approval);

        Log.Information("Approval rejected: {Action} by {RejectedBy} — {Reason}",
            approval.ActionType, rejectedBy, reason);

        return JsonSerializer.Serialize(new
        {
            rejected = true,
            pending_approval_id = idStr,
            action_type = approval.ActionType,
            rejected_by = rejectedBy,
            reason
        });
    }
}

/// <summary>
/// Lists all pending approvals for human review.
/// </summary>
public class ListPendingApprovalsTool
{
    private readonly IStateStore _store;

    public ListPendingApprovalsTool(IStateStore store) => _store = store;

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var pending = await _store.GetPendingApprovalsAsync();

        return JsonSerializer.Serialize(new
        {
            count = pending.Count,
            approvals = pending.Select(a => new
            {
                id = a.Id.ToString(),
                action_type = a.ActionType,
                requested_by = a.RequestedBy,
                details = a.Details,
                created_at = a.CreatedAt
            }).ToList()
        });
    }
}
