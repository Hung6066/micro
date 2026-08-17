using His.Hope.IdentityService.Domain.Entities;

namespace His.Hope.IdentityService.Application.DevicePosture;

public sealed class DevicePosturePolicyEvaluator
{
    public DevicePostureEvaluation Evaluate(DevicePosturePolicy policy, DevicePostureEvidence evidence, DateTime utcNow, ISet<string>? seenHashes = null)
    {
        var normalized = DevicePostureEvidenceNormalizer.Normalize(evidence);
        if (!(System.Text.Json.JsonSerializer.Deserialize<string[]>(policy.ProvidersJson) ?? [])
            .Contains(normalized.Provider, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Posture provider is not enabled by policy.");
        var expiresAt = evidence.ObservedAt.ToUniversalTime().AddSeconds(Math.Clamp(policy.EvidenceTtlSeconds, 60, 3600));
        var fresh = evidence.ObservedAt.ToUniversalTime() <= utcNow && expiresAt > utcNow;
        var replayed = seenHashes?.Contains(normalized.Hash) == true;
        var required = System.Text.Json.JsonSerializer.Deserialize<string[]>(policy.RequiredSignalsJson) ?? [];
        var meets = fresh && !replayed && required.All(signal => normalized.Signals.TryGetValue(signal, out var value) && value);
        var mode = policy.Mode.Trim().ToLowerInvariant() switch { "stepup" => "stepup", "deny" => "deny", _ => "observe" };
        var decision = !meets && mode == "deny" ? "deny" : !meets && mode == "stepup" ? "stepup" : "observe";
        return new DevicePostureEvaluation(decision, fresh && !replayed, meets, normalized.Provider, policy.Version, expiresAt, normalized.Hash);
    }
}
