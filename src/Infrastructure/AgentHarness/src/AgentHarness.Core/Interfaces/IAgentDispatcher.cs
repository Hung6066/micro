using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Core.Interfaces;

public interface IAgentDispatcher
{
    Task<AgentRun> DispatchAsync(AgentRun agentRun, CancellationToken ct = default);
    Task<AgentRun> GetStatusAsync(Guid agentRunId, CancellationToken ct = default);
}
