using His.Hope.IdentityService.Application.Assurance;
using Xunit;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class AssurancePolicyEvaluatorAdditionalTests
{
    private static AssurancePolicyDocument Policy(
        bool freshDevice = false,
        bool breakGlass = false,
        IReadOnlyList<string>? forbiddenRecovery = null) =>
        new(
            "v1",
            ["security"],
            [new AssuranceJourneyPolicy(
                "clinical-read", "aal2", ["mfa", "passkey"], false,
                RequireFreshDevicePosture: freshDevice,
                AllowBreakGlass: breakGlass)],
            new AssuranceRecoveryPolicy(
                ForbidWeakFallbackForHighRisk: true,
                AllowedRecoveryMethods: ["passkey"],
                ForbiddenRecoveryMethods: forbiddenRecovery ?? []));

    [Fact]
    public void Unknown_journey_is_denied()
    {
        var result = AssurancePolicyEvaluator.Evaluate(Policy(), "missing", "aal3");

        Assert.False(result.Allowed);
        Assert.Equal("Unknown assurance journey.", result.Reason);
    }

    [Fact]
    public void Fresh_device_posture_is_required_when_configured()
    {
        var result = AssurancePolicyEvaluator.Evaluate(Policy(freshDevice: true), "clinical-read", "aal2");

        Assert.False(result.Allowed);
        Assert.Equal("Fresh device posture required.", result.Reason);
    }

    [Fact]
    public void Break_glass_is_denied_when_journey_does_not_allow_it()
    {
        var result = AssurancePolicyEvaluator.Evaluate(Policy(), "clinical-read", "aal3", isBreakGlass: true);

        Assert.False(result.Allowed);
        Assert.Equal("Break-glass not permitted for journey.", result.Reason);
    }

    [Fact]
    public void Forbidden_recovery_method_is_denied_case_insensitively()
    {
        var result = AssurancePolicyEvaluator.Evaluate(
            Policy(forbiddenRecovery: ["backup-code"]),
            "clinical-read",
            "aal2",
            recoveryMethod: "BACKUP-CODE");

        Assert.False(result.Allowed);
        Assert.Equal("Recovery method forbidden by policy.", result.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("aal1")]
    public void Assurance_below_minimum_is_denied(string? assurance)
    {
        var result = AssurancePolicyEvaluator.Evaluate(Policy(), "clinical-read", assurance);

        Assert.False(result.Allowed);
        Assert.Equal("Assurance below journey minimum.", result.Reason);
    }

    [Fact]
    public void Sufficient_assurance_and_fresh_posture_are_allowed()
    {
        var result = AssurancePolicyEvaluator.Evaluate(
            Policy(freshDevice: true, breakGlass: true),
            "clinical-read",
            "mtls",
            devicePostureFresh: true,
            isBreakGlass: true);

        Assert.True(result.Allowed);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Empty_policy_json_is_rejected()
    {
        const string json = "{\"version\":\"v1\",\"approvedBy\":[],\"journeys\":[],\"recovery\":{\"forbidWeakFallbackForHighRisk\":true,\"allowedRecoveryMethods\":[],\"forbiddenRecoveryMethods\":[]}}";

        Assert.Throws<InvalidOperationException>(() => AssurancePolicyEvaluator.LoadFromJson(json));
    }
}
