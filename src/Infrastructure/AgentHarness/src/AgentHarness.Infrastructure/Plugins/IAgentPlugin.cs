using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Infrastructure.Plugins;

public interface IAgentPlugin
{
    string Name { get; }
    string Version { get; }
    Task OnAgentStartedAsync(AgentRun agentRun, CancellationToken ct = default);
    Task OnAgentCompletedAsync(AgentRun agentRun, CancellationToken ct = default);
    Task OnAgentFailedAsync(AgentRun agentRun, CancellationToken ct = default);
    Task OnPipelineCompletedAsync(PipelineRun pipelineRun, CancellationToken ct = default);
}
