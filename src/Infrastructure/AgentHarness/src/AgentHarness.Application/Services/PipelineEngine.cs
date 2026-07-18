using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Serilog;
using His.Hope.AgentHarness.Core.Events;

namespace His.Hope.AgentHarness.Application.Services;

/// <summary>
/// Core pipeline execution engine that runs DAG phases in order (Plan → Implement → Test → Validate → Commit),
/// manages concurrency via <see cref="AgentPoolManager"/>, enforces backpressure via
/// <see cref="BackpressureController"/>, and tracks active pipelines with a concurrent dictionary.
/// </summary>
public class PipelineEngine : IPipelineEngine
{
    private static readonly PipelinePhase[] PhaseOrder =
        { PipelinePhase.Plan, PipelinePhase.Implement, PipelinePhase.Test, PipelinePhase.Validate, PipelinePhase.Commit };

    private static readonly ActivitySource ActivitySource = new("His.Hope.AgentHarness", "1.0.0");

    // Active pipeline gauge — tracks number of currently executing pipelines
    private static int _activePipelineCount;

    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activePipelines = new();
    private readonly IAgentDispatcher _dispatcher;
    private readonly IStateStore _store;
    private readonly IEventBus _eventBus;
    private readonly AgentPoolManager _poolManager;
    private readonly BackpressureController _backpressure;
    private readonly ILoopEngineer _loopEngineer;

    public PipelineEngine(
        IAgentDispatcher dispatcher,
        IStateStore store,
        IEventBus eventBus,
        AgentPoolManager poolManager,
        BackpressureController backpressure,
        ILoopEngineer loopEngineer)
    {
        _dispatcher = dispatcher;
        _store = store;
        _eventBus = eventBus;
        _poolManager = poolManager;
        _backpressure = backpressure;
        _loopEngineer = loopEngineer;
    }

    /// <inheritdoc />
    public async Task<PipelineRun> StartAsync(PipelineDag dag, PipelineRun run, CancellationToken ct = default)
    {
        // 1. Backpressure check — throws when pipeline queue is full
        _backpressure.EnsureCapacity();

        // 2. Create linked CancellationTokenSource for this pipeline
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (!_activePipelines.TryAdd(run.Id, cts))
        {
            _backpressure.ReleasePipeline();
            throw new InvalidOperationException($"Pipeline run {run.Id} is already active.");
        }

        // 3. Transition to Running and persist
        run.TransitionTo(PipelineStatus.Running);
        await _store.SavePipelineRunAsync(run, ct);

        // 4. Increment active pipeline gauge
        IncrementActivePipelines();

        // 5. Create tracing activity
        using var activity = ActivitySource.StartActivity("PipelineExecution", ActivityKind.Internal);
        activity?.SetTag("workflow.id", run.WorkflowId);
        activity?.SetTag("pipeline.run.id", run.Id.ToString());

        try
        {
            // 6. Execute DAG with adaptive loop-back support
            await ExecuteDagWithLoopAsync(dag, run, cts.Token);

            // If still Running after all phases, mark as Completed
            if (run.Status == PipelineStatus.Running)
            {
                run.TransitionTo(PipelineStatus.Completed);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
        }
        catch (OperationCanceledException)
        {
            run.TransitionTo(PipelineStatus.Cancelled);
            activity?.SetStatus(ActivityStatusCode.Error, "Cancelled");
        }
        catch (Exception ex)
        {
            run.TransitionTo(PipelineStatus.Failed);
            run.AddMetadata("error", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        }
        finally
        {
            // Always release resources
            _backpressure.ReleasePipeline();
            DecrementActivePipelines();
            _activePipelines.TryRemove(run.Id, out _);
            await _store.SavePipelineRunAsync(run, ct);
        }

        return run;
    }

    private static readonly TimeSpan AgentPollInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Dispatches the agent via <see cref="AgentPoolManager"/> then polls the store
    /// until the agent run reaches a terminal state (Completed, Failed, Cancelled, TimedOut).
    /// External agents (OpenCode) execute the actual work and report back via
    /// <c>complete-task</c> MCP tool.
    /// </summary>
    private async Task<AgentRun> ExecuteNodeAsync(PipelineNode node, PipelineRun run, CancellationToken ct)
    {
        var description = !string.IsNullOrEmpty(node.TaskDescription)
            ? node.TaskDescription
            : $"Executing {node.AgentName} for phase {node.Phase}";
        var agentRun = AgentRun.Create(
            run.Id,
            node.AgentName,
            description,
            maxRetries: 3,
            timeoutSeconds: 600);

        node.StartedAt = DateTime.UtcNow;

        // Dispatch (now returns immediately with Running status)
        var result = await _poolManager.DispatchWithPoolAsync(agentRun, ct);

        // Poll store until terminal
        var deadline = DateTime.UtcNow.AddSeconds(result.TimeoutSeconds);
        while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            var current = await _store.GetAgentRunAsync(result.Id, ct);
            if (current == null)
            {
                Log.Warning("Agent run {RunId} not found in store, breaking poll", result.Id);
                break;
            }

            if (current.IsTerminal())
            {
                result = current;
                break;
            }

            await Task.Delay(AgentPollInterval, ct);
        }

        // Timeout check
        if (!result.IsTerminal())
        {
            result.Timeout();
            await _store.SaveAgentRunAsync(result, ct);
            await _eventBus.PublishAsync(
                new AgentFailed(result.Id, result.PipelineRunId, result.AgentName, "Timed out waiting for external agent", result.RetryCount), ct);
        }

        // Sync node status from agent run result
        node.Status = result.Status switch
        {
            AgentRunStatus.Completed => PipelineStatus.Completed,
            AgentRunStatus.Failed => PipelineStatus.Failed,
            AgentRunStatus.TimedOut => PipelineStatus.Failed,
            AgentRunStatus.Cancelled => PipelineStatus.Cancelled,
            _ => node.Status
        };
        node.CompletedAt = DateTime.UtcNow;

        Log.Information("Agent run {RunId} finished: {AgentName} status={Status} ({Elapsed}ms)",
            result.Id, result.AgentName, result.Status,
            node.StartedAt.HasValue ? (DateTime.UtcNow - node.StartedAt.Value).TotalMilliseconds : 0);

        return result;
    }

    /// <summary>
    /// Executes the DAG phases in order with adaptive loop-back support via the Loop Engineer.
    /// After each phase, quality gates are evaluated. Failed gates trigger Loop Engineer.
    /// </summary>
    private async Task ExecuteDagWithLoopAsync(PipelineDag dag, PipelineRun run, CancellationToken ct)
    {
        const int maxLoops = 3;
        for (int loop = 0; loop < maxLoops; loop++)
        {
            bool anyExecuted = false;

            // Execute all phases
            foreach (var phase in PhaseOrder)
            {
                ct.ThrowIfCancellationRequested();
                if (run.IsTimedOut()) throw new TimeoutException();
                var phaseNodes = dag.GetPhaseNodes(phase).ToList();
                if (!phaseNodes.Any()) continue;

                var tasks = phaseNodes.Select(n => ExecuteNodeAsync(n, run, ct));
                await Task.WhenAll(tasks);
                anyExecuted = true;

                // Evaluate quality gates after this phase
                await EvaluatePhaseGatesAsync(phase, phaseNodes, run, ct);
            }

            if (!anyExecuted) return;

            // Check quality gates
            var gates = await _store.GetQualityGatesAsync(run.Id, ct);
            var failedGates = gates.Where(g => !g.Passed).ToList();
            if (!failedGates.Any()) return; // All passed — success

            Log.Warning("Quality gates failed ({Count}): {Gates}",
                failedGates.Count, string.Join(", ", failedGates.Select(g => g.GateId)));

            // Invoke Loop Engineer
            var loopContext = new LoopContext
            {
                FailedGates = failedGates,
                PreviousIteration = loop
            };
            var fixResult = await _loopEngineer.AnalyzeAndFixAsync(loopContext, ct);

            if (fixResult.Outcome == FixOutcome.AutoFixed || fixResult.Outcome == FixOutcome.PartialFix)
            {
                Log.Information("LoopEngineer {Outcome} on iteration {Loop}, dispatching fix agents",
                    fixResult.Outcome, loop);

                // Reset failed nodes + inject fix agents for re-execution
                foreach (var node in dag.Nodes)
                {
                    if (node.Status != PipelineStatus.Completed)
                    {
                        node.Status = PipelineStatus.Pending;
                        node.AttemptNumber++;
                        node.TaskDescription = $"[Retry {node.AttemptNumber}] {node.TaskDescription}";
                    }
                }

                // Add dedicated fix nodes for each failed gate
                foreach (var gate in failedGates)
                {
                    var fixNode = dag.AddNode("loop-engineer", PipelinePhase.Implement);
                    fixNode.TaskDescription = $"Auto-fix: {gate.GateType} failed — {gate.Details ?? gate.Output ?? "unknown error"}. Iteration {loop + 1}.";
                    fixNode.AttemptNumber = 1;
                    Log.Information("  Fix node added: {Task}", fixNode.TaskDescription);
                }
                continue;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Pipeline blocked after {loop + 1} iterations: {fixResult.EscalationReason}");
            }
        }
        throw new InvalidOperationException("Max pipeline loops reached");
    }

    /// <summary>
    /// Creates quality gates for a completed phase based on agent run results.
    /// </summary>
    private async Task EvaluatePhaseGatesAsync(
        PipelinePhase phase, List<PipelineNode> nodes, PipelineRun run, CancellationToken ct)
    {
        foreach (var node in nodes)
        {
            // Gate 1: Phase/Agent completion
            var passed = node.Status == PipelineStatus.Completed;
            var gate = QualityGate.Create(
                run.Id,
                $"{node.AgentName}-{phase.ToString().ToLower()}",
                $"Agent {node.AgentName} completed phase {phase}",
                passed);

            if (!passed)
            {
                gate.MarkFailed($"Agent {node.AgentName} failed in {phase} phase");
            }

            await _store.SaveQualityGateAsync(gate, ct);
        }

        // Gate 2: Overall phase health check
        var allPassed = nodes.All(n => n.Status == PipelineStatus.Completed);
        var phaseGate = QualityGate.Create(
            run.Id,
            $"phase-{phase.ToString().ToLower()}-complete",
            $"Phase {phase} completion",
            allPassed);

        if (!allPassed)
        {
            phaseGate.MarkFailed($"Phase {phase}: {nodes.Count(n => n.Status != PipelineStatus.Completed)}/{nodes.Count} agents failed");
        }

        await _store.SaveQualityGateAsync(phaseGate, ct);
    }

    /// <inheritdoc />
    public async Task CancelAsync(Guid pipelineRunId, CancellationToken ct = default)
    {
        if (_activePipelines.TryRemove(pipelineRunId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        var run = await _store.GetPipelineRunAsync(pipelineRunId, ct);
        if (run is { Status: PipelineStatus.Running or PipelineStatus.Pending })
        {
            run.TransitionTo(PipelineStatus.Cancelled);
            await _store.SavePipelineRunAsync(run, ct);
        }
    }

    /// <inheritdoc />
    public async Task<PipelineRun> GetStatusAsync(Guid pipelineRunId, CancellationToken ct = default)
    {
        return await _store.GetPipelineRunAsync(pipelineRunId, ct)
               ?? throw new InvalidOperationException($"Pipeline run {pipelineRunId} not found");
    }

    /// <summary>Gets the current number of active pipelines.</summary>
    public static int ActivePipelineCount => Volatile.Read(ref _activePipelineCount);

    private static void IncrementActivePipelines() => Interlocked.Increment(ref _activePipelineCount);
    private static void DecrementActivePipelines() => Interlocked.Decrement(ref _activePipelineCount);
}
