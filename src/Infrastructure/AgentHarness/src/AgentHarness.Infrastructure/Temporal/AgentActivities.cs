using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Temporalio.Activities;
using His.Hope.AgentHarness.Core.Events;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;
using His.Hope.AgentHarness.Infrastructure.Observability;

namespace His.Hope.AgentHarness.Infrastructure.Temporal;

public class AgentActivities : IAgentActivities
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventBus _eventBus;
    private static readonly TimeSpan AgentPollInterval = TimeSpan.FromSeconds(5);

    public AgentActivities(IServiceScopeFactory scopeFactory, IEventBus eventBus)
    {
        _scopeFactory = scopeFactory;
        _eventBus = eventBus;
    }

    public async Task<PhaseResult> ExecutePhaseAsync(Guid pipelineRunId, string phaseName, int loopIteration)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStateStore>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IAgentDispatcher>();

        var run = await store.GetPipelineRunAsync(pipelineRunId);
        if (run?.DagDefinition == null)
            return new PhaseResult { NodeCount = 0, AllCompleted = true };

        var phase = Enum.Parse<PipelinePhase>(phaseName);
        var nodes = run.DagDefinition.GetPhaseNodes(phase).ToList();
        if (nodes.Count == 0)
            return new PhaseResult { NodeCount = 0, AllCompleted = true };

        Log.Information("Temporal: executing phase {Phase} with {Count} nodes (loop {Loop})",
            phaseName, nodes.Count, loopIteration);

        var ctx = ActivityExecutionContext.Current;
        var agentTasks = nodes.Select(async node =>
        {
            var description = !string.IsNullOrEmpty(node.TaskDescription)
                ? node.TaskDescription
                : $"Executing {node.AgentName} for phase {phase}";

            var agentRun = AgentRun.Create(
                pipelineRunId, node.AgentName, description,
                maxRetries: 3, timeoutSeconds: 600);

            node.StartedAt = DateTime.UtcNow;

            // Dispatch
            var dispatched = await dispatcher.DispatchAsync(agentRun, CancellationToken.None);

            // Poll with heartbeats (Temporal will retry from last heartbeat on crash)
            var deadline = DateTime.UtcNow.AddSeconds(dispatched.TimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                ctx.Heartbeat(dispatched);

                var current = await store.GetAgentRunAsync(dispatched.Id);
                if (current == null)
                {
                    Log.Warning("Temporal: agent run {RunId} not found", dispatched.Id);
                    break;
                }

                if (current.IsTerminal())
                {
                    dispatched = current;
                    break;
                }

                await Task.Delay(AgentPollInterval);
            }

            if (!dispatched.IsTerminal())
            {
                dispatched.Timeout();
                await store.SaveAgentRunAsync(dispatched);
            }

            node.Status = dispatched.Status switch
            {
                AgentRunStatus.Completed => PipelineStatus.Completed,
                AgentRunStatus.Failed => PipelineStatus.Failed,
                AgentRunStatus.TimedOut => PipelineStatus.Failed,
                AgentRunStatus.Cancelled => PipelineStatus.Cancelled,
                _ => node.Status
            };
            node.CompletedAt = DateTime.UtcNow;

            Log.Information("Temporal: agent run {RunId} finished: {Agent} status={Status}",
                dispatched.Id, dispatched.AgentName, dispatched.Status);

            return dispatched;
        });

        var results = await Task.WhenAll(agentTasks);
        return new PhaseResult
        {
            NodeCount = nodes.Count,
            AllCompleted = results.All(r => r.Status == AgentRunStatus.Completed)
        };
    }

    public async Task EvaluatePhaseGatesAsync(Guid pipelineRunId, string phaseName)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStateStore>();

        var run = await store.GetPipelineRunAsync(pipelineRunId);
        if (run?.DagDefinition == null) return;

        var phase = Enum.Parse<PipelinePhase>(phaseName);
        var nodes = run.DagDefinition.GetPhaseNodes(phase).ToList();
        if (nodes.Count == 0) return;

        foreach (var node in nodes)
        {
            var passed = node.Status == PipelineStatus.Completed;
            var gate = QualityGate.Create(
                pipelineRunId,
                $"{node.AgentName}-{phaseName.ToLower()}",
                $"Agent {node.AgentName} completed phase {phaseName}",
                passed);

            if (!passed)
                gate.MarkFailed($"Agent {node.AgentName} failed in {phaseName} phase");

            await store.SaveQualityGateAsync(gate);
        }

        var allPassed = nodes.All(n => n.Status == PipelineStatus.Completed);
        var phaseGate = QualityGate.Create(
            pipelineRunId,
            $"phase-{phaseName.ToLower()}-complete",
            $"Phase {phaseName} completion",
            allPassed);

        if (!allPassed)
        {
            var failed = nodes.Count(n => n.Status != PipelineStatus.Completed);
            phaseGate.MarkFailed($"Phase {phaseName}: {failed}/{nodes.Count} agents failed");
        }

        await store.SaveQualityGateAsync(phaseGate);
    }

    public async Task SaveCheckpointAsync(Guid pipelineRunId, string phaseName, int loopIteration)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStateStore>();

        var run = await store.GetPipelineRunAsync(pipelineRunId);
        if (run?.DagDefinition == null) return;

        var completed = run.DagDefinition.Nodes
            .Where(n => n.Status == PipelineStatus.Completed).ToList();
        var failed = run.DagDefinition.Nodes
            .Where(n => n.Status == PipelineStatus.Failed).ToList();

        var checkpoint = PipelineCheckpoint.Create(
            pipelineRunId, phaseName, completed, failed, loopIteration);
        await store.SaveCheckpointAsync(checkpoint);

        Log.Debug("Temporal checkpoint: phase {Phase}, {Completed}/{Total} nodes",
            phaseName, completed.Count, run.DagDefinition.Nodes.Count);
    }

    public async Task<bool> CheckAllGatesPassedAsync(Guid pipelineRunId)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStateStore>();

        var gates = await store.GetQualityGatesAsync(pipelineRunId);
        return gates.All(g => g.Passed);
    }

    public async Task<LoopEngineerResult> RunLoopEngineerAsync(Guid pipelineRunId, int loopIteration)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStateStore>();
        var loopEngineer = scope.ServiceProvider.GetRequiredService<ILoopEngineer>();

        var run = await store.GetPipelineRunAsync(pipelineRunId);
        if (run == null)
            return new LoopEngineerResult { CanContinue = false, EscalationReason = "Pipeline run not found" };

        var gates = await store.GetQualityGatesAsync(pipelineRunId);
        var failedGates = gates.Where(g => !g.Passed).ToList();

        Log.Warning("Temporal: {Count} quality gates failed for pipeline {PipelineId}",
            failedGates.Count, pipelineRunId);

        var loopContext = new LoopContext
        {
            FailedGates = failedGates,
            PreviousIteration = loopIteration
        };

        var fixResult = await loopEngineer.AnalyzeAndFixAsync(loopContext, CancellationToken.None);

        return new LoopEngineerResult
        {
            CanContinue = fixResult.Outcome is FixOutcome.AutoFixed or FixOutcome.PartialFix,
            EscalationReason = fixResult.Outcome is FixOutcome.Escalated or FixOutcome.GiveUp
                ? fixResult.EscalationReason
                : null
        };
    }

    public async Task ResetFailedNodesAsync(Guid pipelineRunId, PipelineDag dag)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStateStore>();

        var run = await store.GetPipelineRunAsync(pipelineRunId);
        if (run == null) return;

        foreach (var node in dag.Nodes)
        {
            if (node.Status != PipelineStatus.Completed)
            {
                node.Status = PipelineStatus.Pending;
                node.AttemptNumber++;
                node.TaskDescription = $"[Retry {node.AttemptNumber}] {node.TaskDescription}";
            }
        }
    }
}
