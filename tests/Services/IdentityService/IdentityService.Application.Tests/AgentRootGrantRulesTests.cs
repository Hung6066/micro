using His.Hope.IdentityService.Application.InfraAccess;
using His.Hope.IdentityService.Domain.Entities;
using Xunit;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class AgentRootGrantRulesTests
{
    private const string ValidKey =
        "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIPnImciBKtlzOkC88dd+OXfF6Qn8WwhNchbO4m8i4ipK openship-agent-root@vmi3129107";

    private static readonly DateTime Now = new(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);
    private static readonly AgentRootOptions Options = new();

    private static AgentRootGrant Pending(Guid requester, DateTime? requestedAt = null) => new()
    {
        Id = Guid.NewGuid(), AgentId = Options.AgentId, Reason = "Rotate node certificates", DurationMinutes = 60,
        Status = AgentRootGrant.StatusPending, RequestedBy = requester, RequestedByEmail = "a@example.test",
        RequestedAt = requestedAt ?? Now.AddMinutes(-5),
    };

    [Theory]
    [InlineData(30, true)]
    [InlineData(480, true)]
    [InlineData(1440, true)]
    [InlineData(29, false)]
    [InlineData(1441, false)]
    public void Duration_must_be_within_configured_bounds(int minutes, bool valid)
    {
        var errors = AgentRootGrantRules.ValidateRequest(minutes, "Rotate node certificates", Options);
        Assert.Equal(valid, !errors.ContainsKey("durationMinutes"));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("too short", false)]
    [InlineData("   ten chars  ", false)]
    [InlineData("exactly 10", true)]
    public void Reason_must_be_10_to_500_characters_after_trimming(string? reason, bool valid)
    {
        var errors = AgentRootGrantRules.ValidateRequest(60, reason, Options);
        Assert.Equal(valid, !errors.ContainsKey("reason"));
        Assert.True(AgentRootGrantRules.ValidateRequest(60, new string('x', 501), Options).ContainsKey("reason"));
    }

    [Fact]
    public void Requester_can_never_approve_their_own_grant()
    {
        var requester = Guid.NewGuid();
        Assert.Equal(AgentRootGrantRules.ApprovalDecision.SelfApproval,
            AgentRootGrantRules.CanApprove(Pending(requester), requester, Now, Options));
        Assert.Equal(AgentRootGrantRules.ApprovalDecision.Allowed,
            AgentRootGrantRules.CanApprove(Pending(requester), Guid.NewGuid(), Now, Options));
    }

    [Fact]
    public void Only_pending_grants_within_the_approval_window_can_be_approved()
    {
        var requester = Guid.NewGuid();
        var stale = Pending(requester, Now.AddMinutes(-Options.PendingTtlMinutes));
        Assert.Equal(AgentRootGrantRules.ApprovalDecision.PendingExpired,
            AgentRootGrantRules.CanApprove(stale, Guid.NewGuid(), Now, Options));

        var issued = Pending(requester);
        issued.Status = AgentRootGrant.StatusIssued;
        Assert.Equal(AgentRootGrantRules.ApprovalDecision.NotPending,
            AgentRootGrantRules.CanApprove(issued, Guid.NewGuid(), Now, Options));
    }

    [Fact]
    public void Active_requires_issued_unrevoked_and_unexpired()
    {
        var grant = Pending(Guid.NewGuid());
        grant.Status = AgentRootGrant.StatusIssued;
        grant.ValidBefore = Now.AddMinutes(30).AddSeconds(-20);
        Assert.True(AgentRootGrantRules.IsActive(grant, Now));
        Assert.Equal(30, AgentRootGrantRules.RemainingMinutes(grant, Now));

        grant.ValidBefore = Now;
        Assert.False(AgentRootGrantRules.IsActive(grant, Now));
        Assert.Equal(0, AgentRootGrantRules.RemainingMinutes(grant, Now));

        grant.ValidBefore = Now.AddHours(1);
        grant.RevokedAt = Now;
        Assert.False(AgentRootGrantRules.IsActive(grant, Now));
    }

    [Theory]
    [InlineData(AgentRootGrant.StatusPending, true)]
    [InlineData(AgentRootGrant.StatusIssued, true)]
    [InlineData(AgentRootGrant.StatusRevoked, false)]
    [InlineData(AgentRootGrant.StatusExpired, false)]
    [InlineData(AgentRootGrant.StatusFailed, false)]
    public void Only_pending_or_issued_grants_can_be_revoked(string status, bool revocable)
    {
        var grant = Pending(Guid.NewGuid());
        grant.Status = status;
        Assert.Equal(revocable, AgentRootGrantRules.CanRevoke(grant));
    }

    [Fact]
    public void Key_id_names_grant_requester_and_approver_and_is_capped()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Assert.Equal("agent-root grant=11111111-2222-3333-4444-555555555555 device=mac-hung requester=a@x.test approver=b@x.test",
            AgentRootGrantRules.BuildKeyId(id, "mac-hung", "a@x.test", " b@x.test "));

        var keyId = AgentRootGrantRules.BuildKeyId(id, "win", new string('r', 300) + "\"quote", "with space@x.test");
        Assert.True(keyId.Length <= AgentRootGrantRules.KeyIdMaxLength);
        Assert.DoesNotContain("\"", keyId);
        Assert.Contains("device=win ", keyId);
        Assert.Contains("approver=", AgentRootGrantRules.BuildKeyId(id, null, null, null));
    }

    [Theory]
    [InlineData(ValidKey, true)]
    [InlineData("ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIPnImciBKtlzOkC88dd+OXfF6Qn8WwhNchbO4m8i4ipK", true)]
    [InlineData("ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQ comment", false)]
    [InlineData("ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIPnImciBKtlzOkC88dd+OXfF6Qn8Wwh", false)]
    [InlineData("ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIPnImciBKtlzOkC88dd+OXfF6Qn8WwhNchbO4m8i4ipK a\nssh-ed25519 x", false)]
    [InlineData("", false)]
    public void Public_key_must_be_one_ed25519_line(string key, bool valid) =>
        Assert.Equal(valid, AgentRootOptions.IsValidPublicKey(key));

    [Fact]
    public void Options_validate_vault_names_and_duration_bounds()
    {
        Assert.Empty(new AgentRootOptions().Validate());
        Assert.Empty(new AgentRootOptions { Enabled = true }.Validate());
        Assert.NotEmpty(new AgentRootOptions { MinMinutes = 60, MaxMinutes = 30 }.Validate());
        Assert.NotEmpty(new AgentRootOptions { VaultMount = "../sys" }.Validate());
    }

    [Theory]
    [InlineData("k3s-manager-vmi3129107", true)]
    [InlineData("mac.hung_2", true)]
    [InlineData("", false)]
    [InlineData("-leading", false)]
    [InlineData("has space", false)]
    public void Device_names_are_key_id_safe(string name, bool valid) =>
        Assert.Equal(valid, AgentRootOptions.IsValidDeviceName(name));

    [Fact]
    public void Fingerprint_matches_ssh_keygen() =>
        Assert.Equal("SHA256:RUmGPD+IkmTBowPW8HKHissVnfpqEt1KiYpKsWjWZsU", AgentRootOptions.Fingerprint(ValidKey));
}
