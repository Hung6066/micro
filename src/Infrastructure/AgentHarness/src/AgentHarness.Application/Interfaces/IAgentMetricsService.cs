using His.Hope.AgentHarness.Application.DTOs;

namespace His.Hope.AgentHarness.Application.Interfaces;

/// <summary>
/// Interface for the agent metrics service, providing access to agent
/// performance profiles including AIS scores and historical data.
/// </summary>
public interface IAgentMetricsService
{
    /// <summary>Gets the performance profile for a specific agent.</summary>
    Task<AgentProfileDto> GetAgentProfileAsync(string agentName, CancellationToken ct = default);
}
