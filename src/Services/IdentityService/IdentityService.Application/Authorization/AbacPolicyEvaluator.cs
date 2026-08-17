using System.Text.Json;

namespace His.Hope.IdentityService.Application.Authorization;

public sealed record AbacPolicyContext(
    string? FacilityId = null,
    string? PurposeOfUse = null,
    bool DevicePostureFresh = false,
    bool IsBreakGlass = false,
    string? Assurance = null);

public sealed record AbacPolicyDecision(bool Allowed, string Reason);

/// <summary>
/// Small fail-closed evaluator for the versioned policy catalog. Domain
/// services remain resource owners; this evaluator only handles shared context
/// attributes and is safe to run in simulation/shadow mode first.
/// </summary>
public static class AbacPolicyEvaluator
{
    private static readonly HashSet<string> AllowedKeys = new(StringComparer.Ordinal)
    {
        "requiredFacility", "allowedPurposeOfUse", "requireFreshDevicePosture", "allowBreakGlass", "requiredAssurance"
    };

    public static bool TryValidate(string rulesJson, out string[] errors)
    {
        var violations = new List<string>();
        try
        {
            using var document = JsonDocument.Parse(rulesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                violations.Add("Rules must be a JSON object.");
            else
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (!AllowedKeys.Contains(property.Name))
                    {
                        violations.Add($"Unknown rule '{property.Name}'.");
                        continue;
                    }
                    if (property.Name is "requiredFacility" or "requireFreshDevicePosture" or "allowBreakGlass" && property.Value.ValueKind != JsonValueKind.True && property.Value.ValueKind != JsonValueKind.False)
                        violations.Add($"Rule '{property.Name}' must be boolean.");
                    if (property.Name == "allowedPurposeOfUse" && (property.Value.ValueKind != JsonValueKind.Array || property.Value.GetArrayLength() == 0 || property.Value.EnumerateArray().Any(value => value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))))
                        violations.Add("Rule 'allowedPurposeOfUse' must be a non-empty string array.");
                    if (property.Name == "requiredAssurance" && (property.Value.ValueKind != JsonValueKind.String || property.Value.GetString() is not ("mfa" or "passkey" or "mtls")))
                        violations.Add("Rule 'requiredAssurance' must be mfa, passkey or mtls.");
                }
            }
        }
        catch (JsonException)
        {
            violations.Add("Rules must be valid JSON.");
        }
        errors = violations.ToArray();
        return errors.Length == 0;
    }

    public static AbacPolicyDecision Evaluate(string rulesJson, AbacPolicyContext context)
    {
        if (!TryValidate(rulesJson, out _)) return new(false, "policy_invalid");
        using var document = JsonDocument.Parse(rulesJson);
        var root = document.RootElement;
        if (root.TryGetProperty("requiredFacility", out var requiredFacility) && requiredFacility.GetBoolean() && string.IsNullOrWhiteSpace(context.FacilityId))
            return new(false, "facility_required");
        if (root.TryGetProperty("allowedPurposeOfUse", out var purposes) && (!purposes.EnumerateArray().Any(item => string.Equals(item.GetString(), context.PurposeOfUse, StringComparison.OrdinalIgnoreCase))))
            return new(false, "purpose_of_use_denied");
        if (root.TryGetProperty("requireFreshDevicePosture", out var posture) && posture.GetBoolean() && !context.DevicePostureFresh)
            return new(false, "device_posture_stale");
        if (root.TryGetProperty("allowBreakGlass", out var breakGlass) && !breakGlass.GetBoolean() && context.IsBreakGlass)
            return new(false, "break_glass_denied");
        if (root.TryGetProperty("requiredAssurance", out var assurance) && !string.Equals(assurance.GetString(), context.Assurance, StringComparison.OrdinalIgnoreCase))
            return new(false, "assurance_insufficient");
        return new(true, "abac_context_match");
    }
}
