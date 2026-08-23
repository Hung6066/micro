using His.Hope.IdentityService.Application.Assurance;
using Xunit;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class AssuranceRecoveryEnforcementTests
{
    private const string PolicyJson = """
        {
          "version": "1.0",
          "approvedBy": ["Security"],
          "journeys": [
            { "id": "clinical-read", "minimumAssurance": "aal2", "allowedFactors": ["passkey"], "stepUpRequired": true, "requireFreshDevicePosture": true }
          ],
          "recovery": {
            "forbidWeakFallbackForHighRisk": true,
            "allowedRecoveryMethods": ["passkey", "admin-assisted"],
            "forbiddenRecoveryMethods": ["email-only"]
          }
        }
        """;

    [Fact]
    public void EvaluateRecovery_denies_email_only_for_clinical_journey()
    {
        var policy = AssurancePolicyEvaluator.LoadFromJson(PolicyJson);
        var service = new AssurancePolicyService(policy);
        var result = service.EvaluateRecovery("email-only", "clinical-read");
        Assert.False(result.Allowed);
    }

    [Fact]
    public void EvaluateRecovery_allows_admin_assisted_recovery()
    {
        var policy = AssurancePolicyEvaluator.LoadFromJson(PolicyJson);
        var service = new AssurancePolicyService(policy);
        var result = service.EvaluateRecovery("admin-assisted", "clinical-read");
        Assert.True(result.Allowed);
    }
}
