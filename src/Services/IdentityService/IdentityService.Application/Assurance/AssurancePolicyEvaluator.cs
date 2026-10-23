using System.Text.Json;

namespace His.Hope.IdentityService.Application.Assurance;

public sealed record AssuranceJourneyPolicy(
    string Id,
    string MinimumAssurance,
    IReadOnlyList<string> AllowedFactors,
    bool StepUpRequired,
    bool RequireFreshDevicePosture = false,
    bool AllowBreakGlass = false,
    int? MaxDurationMinutes = null);

public sealed record AssuranceRecoveryPolicy(
    bool ForbidWeakFallbackForHighRisk,
    IReadOnlyList<string> AllowedRecoveryMethods,
    IReadOnlyList<string> ForbiddenRecoveryMethods);

public sealed record AssurancePolicyDocument(
    string Version,
    IReadOnlyList<string> ApprovedBy,
    IReadOnlyList<AssuranceJourneyPolicy> Journeys,
    AssuranceRecoveryPolicy Recovery);

public sealed record AssuranceEvaluationResult(
    bool Allowed,
    string JourneyId,
    string? RequiredAssurance,
    string? Reason);

public static class AssurancePolicyEvaluator
{
    public static AssurancePolicyDocument LoadFromJson(string json)
    {
        var document = JsonSerializer.Deserialize<AssurancePolicyDocument>(json, JsonOptions())
            ?? throw new InvalidOperationException("Assurance policy document is empty.");
        if (document.Journeys.Count == 0)
            throw new InvalidOperationException("Assurance policy must define at least one journey.");
        if (document.Journeys.Any(journey => journey.StepUpRequired && journey.AllowedFactors.Count == 0))
            throw new InvalidOperationException("Every step-up assurance journey must define allowed authentication factors.");
        return document;
    }

    public static AssuranceEvaluationResult Evaluate(
        AssurancePolicyDocument policy,
        string journeyId,
        string? currentAssurance,
        string? recoveryMethod = null,
        bool devicePostureFresh = false,
        bool isBreakGlass = false,
        bool authenticationFresh = true,
        string? currentFactor = null)
    {
        var journey = policy.Journeys.FirstOrDefault(j =>
            string.Equals(j.Id, journeyId, StringComparison.OrdinalIgnoreCase));
        if (journey is null)
            return new AssuranceEvaluationResult(false, journeyId, null, "Unknown assurance journey.");

        if (journey.RequireFreshDevicePosture && !devicePostureFresh)
            return new AssuranceEvaluationResult(false, journey.Id, journey.MinimumAssurance, "Fresh device posture required.");

        if (isBreakGlass && !journey.AllowBreakGlass)
            return new AssuranceEvaluationResult(false, journey.Id, journey.MinimumAssurance, "Break-glass not permitted for journey.");

        if (journey.StepUpRequired && !authenticationFresh)
            return new AssuranceEvaluationResult(false, journey.Id, journey.MinimumAssurance, "Fresh step-up authentication required.");

        if (!string.IsNullOrWhiteSpace(recoveryMethod) &&
            policy.Recovery.ForbiddenRecoveryMethods.Any(method =>
                string.Equals(method, recoveryMethod, StringComparison.OrdinalIgnoreCase)))
        {
            return new AssuranceEvaluationResult(false, journey.Id, journey.MinimumAssurance, "Recovery method forbidden by policy.");
        }

        if (!string.IsNullOrWhiteSpace(recoveryMethod) &&
            policy.Recovery.AllowedRecoveryMethods.Count > 0 &&
            !policy.Recovery.AllowedRecoveryMethods.Any(method =>
                string.Equals(method, recoveryMethod, StringComparison.OrdinalIgnoreCase)))
        {
            return new AssuranceEvaluationResult(false, journey.Id, journey.MinimumAssurance, "Recovery method is not allowed.");
        }

        if (string.IsNullOrWhiteSpace(currentAssurance) ||
            !MeetsMinimumAssurance(currentAssurance, journey.MinimumAssurance))
        {
            return new AssuranceEvaluationResult(false, journey.Id, journey.MinimumAssurance, "Assurance below journey minimum.");
        }

        if (string.IsNullOrWhiteSpace(recoveryMethod) && journey.AllowedFactors.Count > 0 &&
            (string.IsNullOrWhiteSpace(currentFactor) ||
             !journey.AllowedFactors.Any(factor => string.Equals(factor, currentFactor, StringComparison.OrdinalIgnoreCase))))
            return new AssuranceEvaluationResult(false, journey.Id, journey.MinimumAssurance, "Authentication factor is not allowed for this journey.");

        return new AssuranceEvaluationResult(true, journey.Id, journey.MinimumAssurance, null);
    }

    internal static bool MeetsMinimumAssurance(string current, string minimum) =>
        AssuranceRank(current) >= AssuranceRank(minimum);

    private static int AssuranceRank(string level) => level.ToLowerInvariant() switch
    {
        "aal1" or "standard" => 1,
        "aal2" or "mfa" or "passkey" => 2,
        "aal3" or "mtls" => 3,
        _ => 0
    };

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
