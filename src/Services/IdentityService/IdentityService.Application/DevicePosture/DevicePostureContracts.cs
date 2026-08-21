using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace His.Hope.IdentityService.Application.DevicePosture;

public static class DevicePostureProviders
{
    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "chrome-enterprise",
        "advanced-compliance",
        "windows-local-login",
        "play-integrity",
        "app-attest",
        "firebase-app-check",
    };
}

public sealed record DevicePostureEvidence(
    Guid UserId,
    string DeviceId,
    string Provider,
    IReadOnlyDictionary<string, bool> Signals,
    DateTime ObservedAt,
    string? ReplayNonce = null,
    string? FacilityId = null);

public sealed record DevicePostureEvaluation(
    string Decision,
    bool Fresh,
    bool MeetsRequirements,
    string Provider,
    string PolicyVersion,
    DateTime ExpiresAt,
    string EvidenceHash);

public sealed record DevicePosturePolicyInput(
    string Mode,
    IReadOnlyCollection<string> Providers,
    int EvidenceTtlSeconds,
    IReadOnlyCollection<string> RequiredSignals,
    string? Version = null,
    string? FacilityId = null);

public static class DevicePostureEvidenceNormalizer
{
    private static readonly HashSet<string> SecretKeys = new(StringComparer.OrdinalIgnoreCase)
    { "token", "access_token", "refresh_token", "assertion", "certificate", "private_key", "client_secret" };

    public static (string Provider, string DeviceId, Dictionary<string, bool> Signals, string Hash) Normalize(DevicePostureEvidence input)
    {
        var provider = input.Provider.Trim().ToLowerInvariant();
        var deviceId = input.DeviceId.Trim();
        if (!DevicePostureProviders.Allowed.Contains(provider)) throw new ArgumentException("Unknown posture provider.", nameof(input));
        if (input.UserId == Guid.Empty || deviceId.Length is < 1 or > 256) throw new ArgumentException("User and device identifiers are required.", nameof(input));
        if (input.Signals is null || input.Signals.Count == 0) throw new ArgumentException("At least one posture signal is required.", nameof(input));
        var signals = input.Signals.ToDictionary(pair => pair.Key.Trim().ToLowerInvariant(), pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        if (signals.Keys.Any(key => SecretKeys.Any(secret => key.Contains(secret, StringComparison.OrdinalIgnoreCase))))
            throw new ArgumentException("Raw credentials or attestation material are not accepted.", nameof(input));
        var canonical = JsonSerializer.Serialize(new { input.UserId, deviceId, provider, signals, input.ObservedAt, nonce = input.ReplayNonce });
        return (provider, deviceId, signals, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant());
    }
}
