using System.Text.RegularExpressions;

namespace His.Hope.IdentityService.Application.HostSecurity;

/// <summary>
/// Approving an integrity drift from admin-app Security. The host's integrity check pulls the
/// approved fingerprints and accepts its baseline only when its CURRENT drift has exactly that
/// fingerprint, so an approval never covers changes made after the admin looked at it.
/// Critical drift (sshd, keys, sudoers, accounts, ld.so.preload, the checker) needs a second
/// super-admin: the first approval is a request, a different person confirms.
/// </summary>
public static partial class HostIntegrityApprovalRules
{
    public const int MinReason = 10;
    public const int MaxReason = 300;
    public static readonly TimeSpan PendingTtl = TimeSpan.FromHours(24);
    /// <summary>The host pulls approvals of this age; older ones are history only.</summary>
    public static readonly TimeSpan HostPullWindow = TimeSpan.FromDays(7);

    public static string Key(string host, string fingerprint) => $"drift:{host}:{fingerprint}";

    public static string? Validate(string? host, string? fingerprint, string? reason)
    {
        if (string.IsNullOrWhiteSpace(host) || !HostName().IsMatch(host)) return "invalid_host";
        if (string.IsNullOrWhiteSpace(fingerprint) || !Fingerprint().IsMatch(fingerprint)) return "invalid_fingerprint";
        var text = reason?.Trim() ?? "";
        if (text.Length < MinReason || text.Length > MaxReason) return "reason_length";
        return null;
    }

    public static bool IsValidHost(string? host) => host is { Length: > 0 } && HostName().IsMatch(host);

    public static bool NeedsSecondApprover(string severity) => severity == HostSecuritySeverities.Critical;

    /// <summary>What an "approve" click does given the stored state.</summary>
    public static HostIntegrityApprovalDecision Decide(
        HostIntegrityApprovalState state, Guid actor, string severity, DateTimeOffset now)
    {
        if (state.Approved is not null) return HostIntegrityApprovalDecision.AlreadyApproved;
        var pending = state.Request is { } request && now - request.At < PendingTtl ? request : null;
        if (!NeedsSecondApprover(severity)) return HostIntegrityApprovalDecision.Approve;
        if (pending is null) return HostIntegrityApprovalDecision.Request;
        return pending.By == actor ? HostIntegrityApprovalDecision.SameApprover : HostIntegrityApprovalDecision.Approve;
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9.-]{0,62}$")]
    private static partial Regex HostName();

    [GeneratedRegex(@"^[0-9a-f]{16}$")]
    private static partial Regex Fingerprint();
}

public enum HostIntegrityApprovalDecision
{
    Approve,
    Request,
    SameApprover,
    AlreadyApproved,
}

public sealed record HostIntegrityApprovalStep(Guid By, string ByName, DateTimeOffset At, string Reason);

public sealed record HostIntegrityApprovalState(HostIntegrityApprovalStep? Request, HostIntegrityApprovalStep? Approved);

/// <summary>One approval as the admin page and the host see it.</summary>
public sealed record HostIntegrityApproval(
    string Key,
    string Host,
    string Fingerprint,
    string Status,
    string Severity,
    string Reason,
    string RequestedBy,
    DateTimeOffset RequestedAt,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt);
