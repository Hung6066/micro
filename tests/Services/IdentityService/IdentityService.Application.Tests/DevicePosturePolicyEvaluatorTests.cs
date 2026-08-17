using His.Hope.IdentityService.Application.DevicePosture;
using His.Hope.IdentityService.Domain.Entities;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class DevicePosturePolicyEvaluatorTests
{
    [Fact]
    public void Default_observe_never_denies_login()
    {
        var policy = new DevicePosturePolicy();
        var evidence = Evidence(new Dictionary<string, bool> { ["managed"] = false });

        var result = new DevicePosturePolicyEvaluator().Evaluate(policy, evidence, DateTime.UtcNow);

        Assert.Equal("observe", result.Decision);
        Assert.True(result.Fresh);
    }

    [Fact]
    public void Deny_policy_requires_fresh_required_signals()
    {
        var policy = new DevicePosturePolicy { Mode = "deny", RequiredSignalsJson = "[\"managed\",\"encrypted\"]" };
        var evidence = Evidence(new Dictionary<string, bool> { ["managed"] = true });

        var result = new DevicePosturePolicyEvaluator().Evaluate(policy, evidence, DateTime.UtcNow);

        Assert.Equal("deny", result.Decision);
        Assert.False(result.MeetsRequirements);
    }

    [Fact]
    public void Secret_material_is_rejected_before_persistence()
    {
        var evidence = Evidence(new Dictionary<string, bool> { ["access_token"] = true });

        Assert.Throws<ArgumentException>(() => new DevicePosturePolicyEvaluator().Evaluate(new DevicePosturePolicy(), evidence, DateTime.UtcNow));
    }

    [Fact]
    public void Expired_evidence_is_not_fresh()
    {
        var policy = new DevicePosturePolicy { Mode = "deny", RequiredSignalsJson = "[\"managed\"]" };
        var evidence = Evidence(new Dictionary<string, bool> { ["managed"] = true }, DateTime.UtcNow.AddMinutes(-30));

        var result = new DevicePosturePolicyEvaluator().Evaluate(policy, evidence, DateTime.UtcNow);

        Assert.False(result.Fresh);
        Assert.Equal("deny", result.Decision);
    }

    [Fact]
    public void Disabled_provider_is_rejected_fail_closed()
    {
        var policy = new DevicePosturePolicy { ProvidersJson = "[\"chrome-enterprise\"]" };
        var evidence = Evidence(new Dictionary<string, bool> { ["managed"] = true });

        Assert.Throws<InvalidOperationException>(() =>
            new DevicePosturePolicyEvaluator().Evaluate(policy, evidence, DateTime.UtcNow));
    }

    [Fact]
    public void Replayed_hash_is_not_fresh_and_stepup_mode_requests_stepup()
    {
        var policy = new DevicePosturePolicy { Mode = "stepup", RequiredSignalsJson = "[\"managed\"]" };
        var evidence = Evidence(new Dictionary<string, bool> { ["managed"] = true });
        var normalized = DevicePostureEvidenceNormalizer.Normalize(evidence);

        var result = new DevicePosturePolicyEvaluator().Evaluate(
            policy, evidence, DateTime.UtcNow, new HashSet<string> { normalized.Hash });

        Assert.Equal("stepup", result.Decision);
        Assert.False(result.Fresh);
        Assert.False(result.MeetsRequirements);
    }

    [Fact]
    public void Future_evidence_is_not_fresh_and_ttl_is_clamped()
    {
        var policy = new DevicePosturePolicy { EvidenceTtlSeconds = 1, Mode = "deny" };
        var now = DateTime.UtcNow;
        var evidence = Evidence(new Dictionary<string, bool> { ["managed"] = true }, now.AddMinutes(1));

        var result = new DevicePosturePolicyEvaluator().Evaluate(policy, evidence, now);

        Assert.False(result.Fresh);
        Assert.Equal("deny", result.Decision);
        Assert.Equal(TimeSpan.FromSeconds(60), result.ExpiresAt - evidence.ObservedAt.ToUniversalTime());
    }

    private static DevicePostureEvidence Evidence(IReadOnlyDictionary<string, bool> signals, DateTime? observedAt = null) =>
        new(Guid.NewGuid(), "pilot-device-1", "advanced-compliance", signals, observedAt ?? DateTime.UtcNow, "nonce-1");
}
