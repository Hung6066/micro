using His.Hope.AgentHarness.Core.Events;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;
using His.Hope.AgentHarness.Infrastructure.Observability;

namespace His.Hope.AgentHarness.Infrastructure.Dispatch;

public class OpenCodeAgentDispatcher : IAgentDispatcher
{
    private readonly IEventBus _eventBus;
    private readonly IStateStore _store;

    public OpenCodeAgentDispatcher(IEventBus eventBus, IStateStore store)
    {
        _eventBus = eventBus;
        _store = store;
    }

    public async Task<AgentRun> DispatchAsync(AgentRun agentRun, CancellationToken ct)
    {
        agentRun.Start();
        await _store.SaveAgentRunAsync(agentRun, ct);
        await _eventBus.PublishAsync(
            new AgentStarted(agentRun.Id, agentRun.PipelineRunId, agentRun.AgentName, agentRun.TaskDescription), ct);
        HarnessMetrics.AgentDispatchCount.Add(1);

        try
        {
            // Actual dispatch via MCP tool invocation at the Mcp layer
            await Task.CompletedTask;
        }
        catch (TimeoutException)
        {
            agentRun.Timeout();
            await _store.SaveAgentRunAsync(agentRun, ct);
            await _eventBus.PublishAsync(
                new AgentFailed(agentRun.Id, agentRun.PipelineRunId, agentRun.AgentName, "Timeout", agentRun.RetryCount), ct);
        }
        catch (Exception ex)
        {
            agentRun.Fail(ex.Message);
            await _store.SaveAgentRunAsync(agentRun, ct);
            await _eventBus.PublishAsync(
                new AgentFailed(agentRun.Id, agentRun.PipelineRunId, agentRun.AgentName, ex.Message, agentRun.RetryCount), ct);
        }

        return agentRun;
    }

    public async Task<AgentRun> GetStatusAsync(Guid agentRunId, CancellationToken ct)
        => await _store.GetAgentRunAsync(agentRunId, ct)
           ?? throw new InvalidOperationException($"Agent run {agentRunId} not found");
}
