using System.Security.Claims;
using His.Hope.IdentityService.Application.Assurance;
using Xunit;

namespace His.Hope.IdentityService.Application.Tests;

/// <summary>
/// Branch-focused coverage for the assurance policy evaluator and its service facade.
/// These tests intentionally assert the fail-closed ordering of the policy checks.
/// </summary>
public sealed class AssurancePolicyCoverageTests
{
    [Fact]
    public void LoadFromJson_rejects_null_document()
    {
        var action = () => AssurancePolicyEvaluator.LoadFromJson("null");

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Equal("Assurance policy document is empty.", exception.Message);
    }

    [Fact]
    public void LoadFromJson_rejects_malformed_document()
    {
        Assert.ThrowsAny<System.Text.Json.JsonException>(() =>
            AssurancePolicyEvaluator.LoadFromJson("{\"version\":\"1\""));
    }

    [Fact]
    public void LoadFromJson_rejects_document_without_journeys()
    {
        var action = () => AssurancePolicyEvaluator.LoadFromJson(
            "{\"version\":\"1\",\"approvedBy\":[],\"journeys\":[],\"recovery\":{\"forbidWeakFallbackForHighRisk\":false,\"allowedRecoveryMethods\":[],\"forbiddenRecoveryMethods\":[]}}");

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Equal("Assurance policy must define at least one journey.", exception.Message);
    }

    [Fact]
    public void LoadFromJson_rejects_step_up_journey_without_allowed_factors()
    {
        var action = () => AssurancePolicyEvaluator.LoadFromJson(
            "{\"version\":\"1\",\"approvedBy\":[],\"journeys\":[{\"id\":\"admin-write\",\"minimumAssurance\":\"aal2\",\"allowedFactors\":[],\"stepUpRequired\":true}],\"recovery\":{\"forbidWeakFallbackForHighRisk\":true,\"allowedRecoveryMethods\":[],\"forbiddenRecoveryMethods\":[]}}");

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Equal("Every step-up assurance journey must define allowed authentication factors.", exception.Message);
    }

    [Fact]
    public void Evaluate_unknown_journey_fails_closed()
    {
        var result = AssurancePolicyEvaluator.Evaluate(Policy(), "not-configured", "aal3");

        Assert.False(result.Allowed);
        Assert.Equal("not-configured", result.JourneyId);
        Assert.Null(result.RequiredAssurance);
        Assert.Equal("Unknown assurance journey.", result.Reason);
    }

    [Fact]
    public void Evaluate_requires_fresh_posture_before_other_checks()
    {
        var result = AssurancePolicyEvaluator.Evaluate(
            Policy(), "clinical-read", "aal3", recoveryMethod: "email-only", devicePostureFresh: false);

        Assert.False(result.Allowed);
        Assert.Equal("Fresh device posture required.", result.Reason);
        Assert.Equal("aal2", result.RequiredAssurance);
    }

    [Fact]
    public void Evaluate_denies_break_glass_when_journey_does_not_allow_it()
    {
        var result = AssurancePolicyEvaluator.Evaluate(
            Policy(), "admin-write", "aal2", devicePostureFresh: true, isBreakGlass: true);

        Assert.False(result.Allowed);
        Assert.Equal("Break-glass not permitted for journey.", result.Reason);
    }

    [Fact]
    public void Evaluate_allows_case_insensitive_break_glass_journey()
    {
        var result = AssurancePolicyEvaluator.Evaluate(
            Policy(), "BREAK-GLASS", "passkey", devicePostureFresh: false, isBreakGlass: true, currentFactor: "mfa");

        Assert.True(result.Allowed);
        Assert.Equal("break-glass", result.JourneyId);
        Assert.Null(result.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("aal1")]
    [InlineData("unknown")]
    public void Evaluate_denies_missing_or_insufficient_assurance(string? currentAssurance)
    {
        var result = AssurancePolicyEvaluator.Evaluate(
            Policy(), "admin-write", currentAssurance, devicePostureFresh: true, currentFactor: "mfa");

        Assert.False(result.Allowed);
        Assert.Equal("Assurance below journey minimum.", result.Reason);
    }

    [Theory]
    [InlineData("EMAIL-ONLY")]
    [InlineData("email-only")]
    public void Evaluate_denies_forbidden_recovery_case_insensitively(string recoveryMethod)
    {
        var result = AssurancePolicyEvaluator.Evaluate(
            Policy(), "admin-write", "aal2", recoveryMethod, devicePostureFresh: true, currentFactor: "mfa");

        Assert.False(result.Allowed);
        Assert.Equal("Recovery method forbidden by policy.", result.Reason);
    }

    [Fact]
    public void Evaluate_allows_configured_assurance_and_non_forbidden_recovery()
    {
        var result = AssurancePolicyEvaluator.Evaluate(
            Policy(), "admin-write", "MFA", recoveryMethod: "admin-assisted", devicePostureFresh: true, currentFactor: "mfa");

        Assert.True(result.Allowed);
        Assert.Equal("admin-write", result.JourneyId);
        Assert.Equal("aal2", result.RequiredAssurance);
        Assert.Null(result.Reason);
    }

    [Theory]
    [InlineData("standard", "aal1", true)]
    [InlineData("password", "aal2", false)]
    [InlineData("mfa", "aal2", true)]
    [InlineData("passkey", "aal3", false)]
    [InlineData("mtls", "aal3", true)]
    [InlineData("unknown", "aal1", false)]
    public void Evaluate_uses_known_assurance_aliases_and_fails_closed_for_unknown_levels(
        string currentAssurance, string minimumAssurance, bool expectedAllowed)
    {
        var policy = new AssurancePolicyDocument(
            "test", ["security"],
            [new AssuranceJourneyPolicy("journey", minimumAssurance, [], false)],
            new AssuranceRecoveryPolicy(false, [], []));

        var result = AssurancePolicyEvaluator.Evaluate(policy, "journey", currentAssurance);

        Assert.Equal(expectedAllowed, result.Allowed);
    }

    [Fact]
    public void Policy_service_exposes_policy_and_resolves_journey_claims()
    {
        var policy = Policy();
        var service = new AssurancePolicyService(policy);

        var result = service.EvaluateJourney(
            Principal(
                ("amr", "mfa"),
                ("device_posture_fresh", "1"),
                ("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())),
            "clinical-read");

        Assert.Same(policy, service.Policy);
        Assert.True(result.Allowed);
    }

    [Fact]
    public void Policy_service_denies_stale_device_for_clinical_journey()
    {
        var service = new AssurancePolicyService(Policy());

        var result = service.EvaluateJourney(Principal(("amr", "mfa")), "clinical-read");

        Assert.False(result.Allowed);
        Assert.Equal("Fresh device posture required.", result.Reason);
    }

    [Fact]
    public void Policy_service_denies_old_step_up_authentication()
    {
        var service = new AssurancePolicyService(Policy());
        var oldAuthTime = DateTimeOffset.UtcNow.AddMinutes(-16).ToUnixTimeSeconds().ToString();

        var result = service.EvaluateJourney(
            Principal(("amr", "mfa"), ("auth_time", oldAuthTime)),
            "admin-write");

        Assert.False(result.Allowed);
        Assert.Equal("Fresh step-up authentication required.", result.Reason);
    }

    [Fact]
    public void Policy_service_evaluates_break_glass_flag()
    {
        var service = new AssurancePolicyService(Policy());
        var principal = Principal(("amr", "mfa"));

        var result = service.EvaluateJourney(principal, "admin-write", isBreakGlass: true);

        Assert.False(result.Allowed);
        Assert.Equal("Break-glass not permitted for journey.", result.Reason);
    }

    [Fact]
    public void Policy_service_recovery_uses_default_clinical_journey()
    {
        var service = new AssurancePolicyService(Policy());

        var result = service.EvaluateRecovery("admin-assisted");

        Assert.True(result.Allowed);
        Assert.Equal("clinical-read", result.JourneyId);
    }

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(claim => new Claim(claim.Type, claim.Value)), "test"));

    private static AssurancePolicyDocument Policy() => new(
        "test",
        ["security"],
        [
            new AssuranceJourneyPolicy("admin-write", "aal2", ["mfa"], true),
            new AssuranceJourneyPolicy("clinical-read", "aal2", ["mfa"], true, RequireFreshDevicePosture: true),
            new AssuranceJourneyPolicy("break-glass", "aal2", ["mfa"], true, AllowBreakGlass: true)
        ],
        new AssuranceRecoveryPolicy(true, ["passkey", "admin-assisted"], ["email-only"]));
}
