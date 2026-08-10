namespace His.Hope.AgentHarness.Application.Services;

/// <summary>
/// Utility for attributing quality gates to agents using unambiguous
/// token-boundary matching instead of substring matching.
///
/// GateId conventions:
///   - Primary: "{agentName}-{phase}" where {phase} is a single token
///     (e.g., "dotnet-implement", "angular-test" — phase values come
///     from the <see cref="PipelinePhase"/> enum: plan, implement,
///     test, validate, commit).
///   - Secondary: "agent-{agentName}" (e.g., "agent-dotnet",
///     "agent-angular-agent") produced by DispatchAgentHandler.
///
/// IMPORTANT: This helper handles overlapping agent names (e.g.,
/// "angular" vs "angular-agent") by verifying that after stripping
/// "{agentName}-" the remainder is a single token. A gate
/// "angular-agent-lint" will NOT be attributed to agent "angular"
/// because the remainder "agent-lint" contains a hyphen.
/// </summary>
internal static class GateAttributionHelper
{
    /// <summary>
    /// Determines whether a quality gate belongs to the specified agent
    /// using exact token-boundary matching.
    /// </summary>
    public static bool GateBelongsToAgent(string gateId, string agentName)
    {
        if (string.IsNullOrEmpty(gateId) || string.IsNullOrEmpty(agentName))
            return false;

        // Format 1: "{agentName}-{phase}" — phase is a single token.
        // We match the agent name prefix and verify the remainder (the
        // phase) contains no hyphens. This prevents false attribution
        // when one agent name is a prefix of another.
        // Example: gate "angular-agent-lint" starts with "angular-" but
        // remainder "agent-lint" has a hyphen → not attributed to "angular".
        var format1Prefix = agentName + "-";
        if (gateId.StartsWith(format1Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var remainder = gateId.Substring(format1Prefix.Length);
            return !remainder.Contains('-');
        }

        // Format 2: "agent-{agentName}" — exact match after "agent-" prefix.
        // This format has no ambiguity because "agent-" is a fixed prefix
        // and the agent name follows verbatim.
        const string agentPrefix = "agent-";
        if (gateId.StartsWith(agentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var gateAgent = gateId.Substring(agentPrefix.Length);
            return string.Equals(gateAgent, agentName, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
