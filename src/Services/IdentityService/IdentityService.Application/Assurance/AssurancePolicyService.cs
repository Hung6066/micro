using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace His.Hope.IdentityService.Application.Assurance;

public sealed class AssurancePolicyService
{
    private readonly AssurancePolicyDocument _policy;

    public AssurancePolicyService(AssurancePolicyDocument policy) => _policy = policy;

    public AssurancePolicyDocument Policy => _policy;

    public AssuranceEvaluationResult EvaluateJourney(ClaimsPrincipal principal, string journeyId, bool isBreakGlass = false)
    {
        var journey = _policy.Journeys.FirstOrDefault(item =>
            string.Equals(item.Id, journeyId, StringComparison.OrdinalIgnoreCase));
        var authenticationFresh = journey is null || !journey.StepUpRequired ||
            AssuranceClaimResolver.HasFreshAuthentication(
                principal,
                journey.MaxDurationMinutes ?? 15);

        return AssurancePolicyEvaluator.Evaluate(
            _policy,
            journeyId,
            AssuranceClaimResolver.ResolveAssuranceLevel(principal),
            devicePostureFresh: AssuranceClaimResolver.HasFreshDevicePosture(principal),
            isBreakGlass: isBreakGlass,
            authenticationFresh: authenticationFresh,
            currentFactor: AssuranceClaimResolver.ResolveStrongestFactor(principal));
    }

    public AssuranceEvaluationResult EvaluateRecovery(string recoveryMethod, string journeyId = "clinical-read") =>
        AssurancePolicyEvaluator.Evaluate(
            _policy,
            journeyId,
            currentAssurance: "aal2",
            recoveryMethod: recoveryMethod,
            devicePostureFresh: true,
            currentFactor: recoveryMethod.Equals("passkey", StringComparison.OrdinalIgnoreCase) ? "passkey" : "mfa");

    public static AssurancePolicyDocument LoadConfiguredPolicy(IConfiguration configuration, IHostEnvironment environment)
    {
        foreach (var candidate in ResolveCandidatePaths(configuration, environment))
        {
            if (!File.Exists(candidate))
                continue;
            return AssurancePolicyEvaluator.LoadFromJson(File.ReadAllText(candidate));
        }

        throw new InvalidOperationException(
            $"Assurance policy file was not found. ContentRoot={environment.ContentRootPath}");
    }

    private static IEnumerable<string> ResolveCandidatePaths(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["Assurance:PolicyPath"];
        if (!string.IsNullOrWhiteSpace(configured))
            yield return configured;

        yield return Path.Combine(environment.ContentRootPath, "config", "assurance-policy.v1.json");

        var current = new DirectoryInfo(environment.ContentRootPath);
        for (var depth = 0; depth < 8 && current is not null; depth++, current = current.Parent)
        {
            var repoCandidate = Path.Combine(current.FullName, "config", "assurance-policy.v1.json");
            yield return repoCandidate;
        }
    }
}
