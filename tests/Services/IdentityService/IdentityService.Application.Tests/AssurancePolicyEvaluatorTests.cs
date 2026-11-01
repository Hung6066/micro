using His.Hope.IdentityService.Application.Assurance;
using Xunit;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class AssurancePolicyEvaluatorTests
{
    private const string PolicyJson = """
        {
          "version": "1.0",
          "approvedBy": ["Security"],
          "journeys": [
            {
              "id": "clinical-read",
              "minimumAssurance": "aal2",
              "allowedFactors": ["passkey", "mfa"],
              "stepUpRequired": true,
              "requireFreshDevicePosture": true
            },
            {
              "id": "break-glass",
              "minimumAssurance": "aal2",
              "allowedFactors": ["mfa"],
              "stepUpRequired": true,
              "allowBreakGlass": true
            }
          ],
          "recovery": {
            "forbidWeakFallbackForHighRisk": true,
            "allowedRecoveryMethods": ["passkey"],
            "forbiddenRecoveryMethods": ["email-only"]
          }
        }
        """;

    [Fact]
    public void Evaluate_AllowsClinicalRead_WhenAssuranceAndPostureMeetMinimum()
    {
        var policy = AssurancePolicyEvaluator.LoadFromJson(PolicyJson);
        var result = AssurancePolicyEvaluator.Evaluate(
            policy,
            "clinical-read",
            "aal2",
            devicePostureFresh: true,
            authenticationFresh: true,
            currentFactor: "mfa");
        Assert.True(result.Allowed);
    }

    [Fact]
    public void Evaluate_DeniesStepUp_WhenAuthenticationIsNotFresh()
    {
        var policy = AssurancePolicyEvaluator.LoadFromJson(PolicyJson);
        var result = AssurancePolicyEvaluator.Evaluate(
            policy,
            "clinical-read",
            "aal2",
            devicePostureFresh: true,
            authenticationFresh: false,
            currentFactor: "mfa");

        Assert.False(result.Allowed);
        Assert.Equal("Fresh step-up authentication required.", result.Reason);
    }

    [Fact]
    public void Evaluate_DeniesClinicalRead_WhenDevicePostureIsStale()
    {
        var policy = AssurancePolicyEvaluator.LoadFromJson(PolicyJson);
        var result = AssurancePolicyEvaluator.Evaluate(policy, "clinical-read", "aal2", devicePostureFresh: false, currentFactor: "mfa");
        Assert.False(result.Allowed);
        Assert.Contains("posture", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_DeniesForbiddenRecoveryMethod()
    {
        var policy = AssurancePolicyEvaluator.LoadFromJson(PolicyJson);
        var result = AssurancePolicyEvaluator.Evaluate(policy, "clinical-read", "aal2", recoveryMethod: "email-only", devicePostureFresh: true, currentFactor: "mfa");
        Assert.False(result.Allowed);
    }

    [Fact]
    public void Evaluate_DeniesFactorOutsideJourneyAllowList()
    {
        var policy = AssurancePolicyEvaluator.LoadFromJson(PolicyJson);
        var result = AssurancePolicyEvaluator.Evaluate(
            policy,
            "clinical-read",
            "aal2",
            devicePostureFresh: true,
            authenticationFresh: true,
            currentFactor: "password");

        Assert.False(result.Allowed);
        Assert.Equal("Authentication factor is not allowed for this journey.", result.Reason);
    }
}
