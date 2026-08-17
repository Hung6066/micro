using FluentAssertions;
using His.Hope.IdentityService.Application.Authorization;
using Xunit;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class AbacPolicyEvaluatorTests
{
    [Fact]
    public void Rejects_unknown_rule_keys()
    {
        AbacPolicyEvaluator.TryValidate("{\"unknown\":true}", out var errors).Should().BeFalse();
        errors.Should().Contain(error => error.Contains("Unknown rule", StringComparison.Ordinal));
    }

    [Fact]
    public void Denies_stale_device_when_policy_requires_fresh_posture()
    {
        var decision = AbacPolicyEvaluator.Evaluate("{\"requireFreshDevicePosture\":true}", new AbacPolicyContext());
        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be("device_posture_stale");
    }

    [Fact]
    public void Allows_matching_facility_purpose_and_assurance()
    {
        var decision = AbacPolicyEvaluator.Evaluate(
            "{\"requiredFacility\":true,\"allowedPurposeOfUse\":[\"treatment\"],\"requiredAssurance\":\"mfa\"}",
            new AbacPolicyContext("facility-a", "treatment", Assurance: "mfa"));
        decision.Should().Be(new AbacPolicyDecision(true, "abac_context_match"));
    }

    [Theory]
    [InlineData("not-json", "Rules must be valid JSON.")]
    [InlineData("[]", "Rules must be a JSON object.")]
    [InlineData("{\"requiredFacility\":\"yes\"}", "Rule 'requiredFacility' must be boolean.")]
    [InlineData("{\"allowedPurposeOfUse\":[]}", "Rule 'allowedPurposeOfUse' must be a non-empty string array.")]
    [InlineData("{\"requiredAssurance\":\"password\"}", "Rule 'requiredAssurance' must be mfa, passkey or mtls.")]
    public void Rejects_malformed_or_invalid_rules(string rules, string expectedError)
    {
        AbacPolicyEvaluator.TryValidate(rules, out var errors).Should().BeFalse();
        errors.Should().Contain(expectedError);
    }

    [Theory]
    [InlineData("{\"requiredFacility\":true}", "facility_required")]
    [InlineData("{\"allowedPurposeOfUse\":[\"treatment\"]}", "purpose_of_use_denied")]
    [InlineData("{\"allowBreakGlass\":false}", "break_glass_denied")]
    [InlineData("{\"requiredAssurance\":\"passkey\"}", "assurance_insufficient")]
    public void Denies_context_mismatches(string rules, string expectedReason)
    {
        var context = new AbacPolicyContext(
            FacilityId: null,
            PurposeOfUse: "operations",
            DevicePostureFresh: true,
            IsBreakGlass: true,
            Assurance: "mfa");

        var decision = AbacPolicyEvaluator.Evaluate(rules, context);

        decision.Allowed.Should().BeFalse();
        decision.Reason.Should().Be(expectedReason);
    }

    [Fact]
    public void Invalid_policy_evaluation_fails_closed()
    {
        AbacPolicyEvaluator.Evaluate("{\"unknown\":true}", new AbacPolicyContext())
            .Should().Be(new AbacPolicyDecision(false, "policy_invalid"));
    }
}
