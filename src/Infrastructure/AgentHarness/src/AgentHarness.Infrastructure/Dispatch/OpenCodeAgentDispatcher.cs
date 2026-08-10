using Serilog;
using His.Hope.AgentHarness.Core.Events;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;
using His.Hope.AgentHarness.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.AgentHarness.Infrastructure.Dispatch;

/// <summary>
/// Dispatches agent runs WITHOUT executing them inline.
/// Sets status to Running, persists, and returns immediately.
/// External agents (OpenCode Angular, .NET, etc.) poll via
/// <c>get-pending-tasks</c> and report completion via <c>complete-task</c>.
/// The <see cref="Application.Services.PipelineEngine"/> polls the store
/// until the agent run reaches a terminal state.
/// </summary>
public class OpenCodeAgentDispatcher : IAgentDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventBus _eventBus;

    public OpenCodeAgentDispatcher(IServiceScopeFactory scopeFactory, IEventBus eventBus)
    {
        _scopeFactory = scopeFactory;
        _eventBus = eventBus;
    }

    public async Task<AgentRun> DispatchAsync(AgentRun agentRun, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStateStore>();

        agentRun.Start();
        await store.SaveAgentRunAsync(agentRun, ct);
        await _eventBus.PublishAsync(
            new AgentStarted(agentRun.Id, agentRun.PipelineRunId, agentRun.AgentName, agentRun.TaskDescription), ct);
        HarnessMetrics.AgentDispatchCount.Add(1);

        Log.Information("Agent dispatched (external execution): {AgentName} | {TaskDescription} | run={Id}",
            agentRun.AgentName, agentRun.TaskDescription, agentRun.Id);

        return agentRun;
    }

    public async Task<AgentRun> GetStatusAsync(Guid agentRunId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStateStore>();
        return await store.GetAgentRunAsync(agentRunId, ct)
               ?? throw new InvalidOperationException($"Agent run {agentRunId} not found");
    }
}
