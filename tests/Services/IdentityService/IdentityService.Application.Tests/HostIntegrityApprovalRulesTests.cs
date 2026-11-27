using FluentAssertions;
using His.Hope.IdentityService.Application.HostSecurity;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class HostIntegrityApprovalRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();
    private static readonly HostIntegrityApprovalState Empty = new(null, null);

    [Theory]
    [InlineData("vmi3129107", "3dec450c11effb93", "planned sudoers change", null)]
    [InlineData("bad host", "3dec450c11effb93", "planned sudoers change", "invalid_host")]
    [InlineData("vmi3129107", "3DEC450C11EFFB93", "planned sudoers change", "invalid_fingerprint")]
    [InlineData("vmi3129107", "3dec450c", "planned sudoers change", "invalid_fingerprint")]
    [InlineData("vmi3129107", "3dec450c11effb93", "short", "reason_length")]
    public void Validates_input(string host, string fingerprint, string reason, string? expected) =>
        HostIntegrityApprovalRules.Validate(host, fingerprint, reason).Should().Be(expected);

    [Fact]
    public void Warning_drift_is_approved_by_one_admin() =>
        HostIntegrityApprovalRules.Decide(Empty, Alice, "warning", Now).Should().Be(HostIntegrityApprovalDecision.Approve);

    [Fact]
    public void Critical_drift_needs_a_second_different_admin()
    {
        HostIntegrityApprovalRules.Decide(Empty, Alice, "critical", Now).Should().Be(HostIntegrityApprovalDecision.Request);
        var requested = new HostIntegrityApprovalState(new HostIntegrityApprovalStep(Alice, "alice", Now.AddMinutes(-5), "reason"), null);
        HostIntegrityApprovalRules.Decide(requested, Alice, "critical", Now).Should().Be(HostIntegrityApprovalDecision.SameApprover);
        HostIntegrityApprovalRules.Decide(requested, Bob, "critical", Now).Should().Be(HostIntegrityApprovalDecision.Approve);
    }

    [Fact]
    public void Stale_requests_start_over_and_approved_drift_stays_approved()
    {
        var stale = new HostIntegrityApprovalState(new HostIntegrityApprovalStep(Alice, "alice", Now.AddHours(-25), "reason"), null);
        HostIntegrityApprovalRules.Decide(stale, Bob, "critical", Now).Should().Be(HostIntegrityApprovalDecision.Request);
        var done = new HostIntegrityApprovalState(null, new HostIntegrityApprovalStep(Bob, "bob", Now, "reason"));
        HostIntegrityApprovalRules.Decide(done, Alice, "critical", Now).Should().Be(HostIntegrityApprovalDecision.AlreadyApproved);
    }
}
