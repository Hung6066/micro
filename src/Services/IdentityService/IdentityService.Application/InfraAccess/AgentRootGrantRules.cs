using His.Hope.IdentityService.Domain.Entities;

namespace His.Hope.IdentityService.Application.InfraAccess;

/// <summary>Pure decision logic for agent root grants, kept apart from HTTP and EF for unit tests.</summary>
public static class AgentRootGrantRules
{
    public const int ReasonMinLength = 10;
    public const int ReasonMaxLength = 500;

    /// <summary>Vault stores key_id in the certificate; keep it short and printable.</summary>
    public const int KeyIdMaxLength = 200;

    public enum ApprovalDecision
    {
        Allowed,
        SelfApproval,
        NotPending,
        PendingExpired,
    }

    /// <summary>Returns a validation message per invalid field; empty when the request is acceptable.</summary>
    public static Dictionary<string, string[]> ValidateRequest(int durationMinutes, string? reason, AgentRootOptions options)
    {
        var errors = new Dictionary<string, string[]>();
        if (durationMinutes < options.MinMinutes || durationMinutes > options.MaxMinutes)
            errors["durationMinutes"] = [$"durationMinutes must be between {options.MinMinutes} and {options.MaxMinutes}."];
        var trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length is < ReasonMinLength or > ReasonMaxLength)
            errors["reason"] = [$"reason must be between {ReasonMinLength} and {ReasonMaxLength} characters."];
        return errors;
    }

    public static ApprovalDecision CanApprove(AgentRootGrant grant, Guid approverId, DateTime nowUtc, AgentRootOptions options)
    {
        // Four-eyes comes first so a self-approval attempt is always reported as such.
        if (grant.RequestedBy == approverId) return ApprovalDecision.SelfApproval;
        if (grant.Status != AgentRootGrant.StatusPending) return ApprovalDecision.NotPending;
        if (IsPendingExpired(grant, nowUtc, options)) return ApprovalDecision.PendingExpired;
        return ApprovalDecision.Allowed;
    }

    public static bool IsPendingExpired(AgentRootGrant grant, DateTime nowUtc, AgentRootOptions options) =>
        grant.Status == AgentRootGrant.StatusPending &&
        grant.RequestedAt.AddMinutes(options.PendingTtlMinutes) <= nowUtc;

    public static bool IsActive(AgentRootGrant grant, DateTime nowUtc) =>
        grant.Status == AgentRootGrant.StatusIssued &&
        grant.RevokedAt is null &&
        grant.ValidBefore is { } validBefore && validBefore > nowUtc;

    /// <summary>Whole minutes left on an active grant, rounded up; 0 otherwise.</summary>
    public static int RemainingMinutes(AgentRootGrant grant, DateTime nowUtc) =>
        IsActive(grant, nowUtc) ? (int)Math.Ceiling((grant.ValidBefore!.Value - nowUtc).TotalMinutes) : 0;

    public static bool CanRevoke(AgentRootGrant grant) =>
        grant.Status is AgentRootGrant.StatusPending or AgentRootGrant.StatusIssued;

    /// <summary>
    /// key_id recorded in the certificate (and therefore in sshd logs on every node). Emails are
    /// stripped to printable ASCII without spaces or quotes and the whole value is capped.
    /// </summary>
    public static string BuildKeyId(Guid grantId, string? deviceName, string? requesterEmail, string? approverEmail)
    {
        var value = $"agent-root grant={grantId:D} device={Clean(deviceName)} requester={Clean(requesterEmail)} approver={Clean(approverEmail)}";
        return value.Length <= KeyIdMaxLength ? value : value[..KeyIdMaxLength];
    }

    private static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        var chars = value.Trim().Where(ch => ch is > ' ' and < '\x7F' and not '"' and not '\'' and not '\\').Take(64).ToArray();
        return chars.Length == 0 ? "unknown" : new string(chars);
    }
}
