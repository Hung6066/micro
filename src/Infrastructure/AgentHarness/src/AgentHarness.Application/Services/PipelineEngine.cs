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
    private readonly AdaptiveQualityGates _adaptiveGates;

    public PipelineEngine(
        IAgentDispatcher dispatcher,
        IStateStore store,
        IEventBus eventBus,
        AgentPoolManager poolManager,
        BackpressureController backpressure,
        ILoopEngineer loopEngineer,
        AdaptiveQualityGates adaptiveGates)
    {
        _dispatcher = dispatcher;
        _store = store;
        _eventBus = eventBus;
        _poolManager = poolManager;
        _backpressure = backpressure;
        _loopEngineer = loopEngineer;
        _adaptiveGates = adaptiveGates;
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

        // 5. Store initial advisory risk metadata (does not affect gate logic)
        await StoreRiskMetadataAsync(dag, run, ct);
        await _store.SavePipelineRunAsync(run, ct);

        // 6. Create tracing activity
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
        using var activity = ActivitySource.StartActivity("AgentExecution", ActivityKind.Internal);
        activity?.SetTag("agent_name", node.AgentName);
        activity?.SetTag("pipeline_id", run.Id.ToString());
        activity?.SetTag("phase", node.Phase.ToString());
        activity?.SetTag("node_id", node.Id.ToString());

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

                // Save checkpoint for crash recovery
                await SaveCheckpointAsync(phase, dag, run, loop, ct);

                // Evaluate quality gates after this phase
                await EvaluatePhaseGatesAsync(phase, phaseNodes, run, ct);
            }

            if (!anyExecuted) return;

            // Check quality gates
            var gates = await _store.GetQualityGatesAsync(run.Id, ct);
            var failedGates = gates.Where(g => !g.Passed).ToList();

            // Store updated advisory risk metadata (does not affect gate logic)
            await StoreRiskMetadataAsync(dag, run, ct);
            await _store.SavePipelineRunAsync(run, ct);

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

    /// <inheritdoc />
    public async Task<PipelineRun> ResumeAsync(PipelineRun run, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("PipelineResume", ActivityKind.Internal);
        activity?.SetTag("pipeline.run.id", run.Id.ToString());
        activity?.SetTag("workflow.id", run.WorkflowId);

        var checkpoint = await _store.GetLatestCheckpointAsync(run.Id, ct);
        if (checkpoint == null)
        {
            Log.Warning("No checkpoint found for pipeline {PipelineId}, starting fresh", run.Id);
            activity?.SetTag("resume.from_checkpoint", false);
            var dag = BuildDagFromRun(run);
            return await StartAsync(dag, run, ct);
        }

        activity?.SetTag("resume.from_checkpoint", true);
        activity?.SetTag("resume.phase", checkpoint.Phase);
        activity?.SetTag("resume.iteration", checkpoint.LoopIteration);

        Log.Information("Resuming pipeline {PipelineId} from phase {Phase}, iteration {Iter}",
            run.Id, checkpoint.Phase, checkpoint.LoopIteration);

        // Build DAG and match completed nodes by agent+phase+task (not ID, which changes after restart)
        var resumeDag = BuildDagFromRun(run);
        var completedAgentKeys = checkpoint.GetCompletedNodeIds();  // stored as "agent|phase|task" combo keys
        foreach (var node in resumeDag.Nodes)
        {
            var key = $"{node.AgentName}|{node.Phase}|{node.TaskDescription}";
            if (completedAgentKeys.Contains(key))
                node.Status = PipelineStatus.Completed;
        }

        run.TransitionTo(PipelineStatus.Running);
        await _store.SavePipelineRunAsync(run, ct);

        return await ResumeFromCheckpoint(resumeDag, run, checkpoint, ct);
    }

    /// <summary>
    /// Rebuilds a DAG from saved parameters or metadata.
    /// </summary>
    private static PipelineDag BuildDagFromRun(PipelineRun run)
    {
        var dag = new PipelineDag();
        var tasksJson = run.Parameters.GetValueOrDefault("tasks")
            ?? run.Metadata.GetValueOrDefault("resolved_tasks");
        if (string.IsNullOrEmpty(tasksJson)) return dag;

        try
        {
            var tasks = System.Text.Json.JsonSerializer.Deserialize<List<TaskDef>>(NormalizeJsonArray(tasksJson),
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (tasks != null)
            {
                foreach (var t in tasks)
                {
                    var phase = Enum.TryParse<PipelinePhase>(t.Phase, ignoreCase: true, out var p) ? p : PipelinePhase.Implement;
                    var node = dag.AddNode(t.Agent, phase);
                    node.TaskDescription = t.Task;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to parse tasks for DAG rebuild");
        }
        return dag;
    }

    private static string NormalizeJsonArray(string raw)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return NormalizeJsonArray(doc.RootElement.GetString() ?? raw);
            }

            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                return $"[{raw}]";
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Let the caller's deserialize surface the original parse error.
        }

        return raw;
    }

    private class TaskDef
    {
        public string Agent { get; set; } = "dotnet";
        public string Task { get; set; } = "";
        public string Phase { get; set; } = "Implement";
    }

    /// <summary>
    /// Resumes execution from a checkpoint, skipping completed nodes.
    /// </summary>
    private async Task<PipelineRun> ResumeFromCheckpoint(
        PipelineDag dag, PipelineRun run, PipelineCheckpoint checkpoint, CancellationToken ct)
    {
        int startIndex = Array.FindIndex(PhaseOrder, p => p.ToString() == checkpoint.Phase);
        if (startIndex < 0) startIndex = 0;

        var completedIds = checkpoint.GetCompletedNodeIds();
        bool anyExecuted = false;

        for (int loop = checkpoint.LoopIteration; loop < 3; loop++)
        {
            startIndex = loop > checkpoint.LoopIteration ? 0 : startIndex;

            for (int i = startIndex; i < PhaseOrder.Length; i++)
            {
                var phase = PhaseOrder[i];
                ct.ThrowIfCancellationRequested();
                if (run.IsTimedOut()) throw new TimeoutException();

                var phaseNodes = dag.GetPhaseNodes(phase)
                    .Where(n => !completedIds.Contains($"{n.AgentName}|{n.Phase}|{n.TaskDescription}"))
                    .ToList();
                if (!phaseNodes.Any()) continue;

                Log.Information("Resume: executing phase {Phase} with {Count} pending nodes", phase, phaseNodes.Count);

                // Reuse existing running agents instead of creating duplicates
                var existingAgents = await _store.GetAgentRunsAsync(run.Id, ct);
                var runningAgents = existingAgents
                    .Where(a => !a.IsTerminal())
                    .ToDictionary(a => a.AgentName, a => a.Id);

                foreach (var node in phaseNodes)
                {
                    if (runningAgents.TryGetValue(node.AgentName, out var existingId))
                    {
                        Log.Information("Resume: reusing existing run {RunId} for {Agent}", existingId, node.AgentName);
                        // Poll the existing run
                        await PollAgentRunAsync(existingId, node, run, ct);
                    }
                    else
                    {
                        // Dispatch fresh
                        await ExecuteNodeAsync(node, run, ct);
                    }
                }
                anyExecuted = true;
                await SaveCheckpointAsync(phase, dag, run, loop, ct);
                await EvaluatePhaseGatesAsync(phase, phaseNodes, run, ct);
            }

            completedIds.Clear();

            var gates = await _store.GetQualityGatesAsync(run.Id, ct);
            var failedGates = gates.Where(g => !g.Passed).ToList();
            if (!failedGates.Any()) break;

            var loopContext = new LoopContext { FailedGates = failedGates, PreviousIteration = loop };
            var fixResult = await _loopEngineer.AnalyzeAndFixAsync(loopContext, ct);
            if (fixResult.Outcome is FixOutcome.Escalated or FixOutcome.GiveUp)
                throw new InvalidOperationException($"Pipeline blocked: {fixResult.EscalationReason}");
        }

        if (!anyExecuted)
        {
            Log.Information("Resume: all nodes already completed for pipeline {PipelineId}", run.Id);
        }

        if (run.Status == PipelineStatus.Running)
        {
            run.TransitionTo(PipelineStatus.Completed);
            await _store.SavePipelineRunAsync(run, ct);
        }

        return run;
    }

    /// <summary>
    /// Polls an existing agent run until terminal — used during resume to avoid duplicate dispatches.
    /// </summary>
    private async Task PollAgentRunAsync(Guid agentRunId, PipelineNode node, PipelineRun run, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(600);
        AgentRun? result = null;

        while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            var current = await _store.GetAgentRunAsync(agentRunId, ct);
            if (current == null) break;
            if (current.IsTerminal()) { result = current; break; }
            await Task.Delay(AgentPollInterval, ct);
        }

        if (result == null)
        {
            result = await _store.GetAgentRunAsync(agentRunId, ct);
            if (result == null || !result.IsTerminal())
            {
                Log.Warning("Resume poll timed out for agent run {RunId}, marking failed", agentRunId);
                result?.Fail("Resume poll timed out");
                if (result != null) await _store.SaveAgentRunAsync(result, ct);
            }
        }

        if (result != null)
        {
            node.Status = result.Status switch
            {
                AgentRunStatus.Completed => PipelineStatus.Completed,
                AgentRunStatus.Failed => PipelineStatus.Failed,
                AgentRunStatus.TimedOut => PipelineStatus.Failed,
                AgentRunStatus.Cancelled => PipelineStatus.Cancelled,
                _ => node.Status
            };
            node.CompletedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Saves a checkpoint snapshot for crash recovery.
    /// </summary>
    private async Task SaveCheckpointAsync(PipelinePhase phase, PipelineDag dag, PipelineRun run, int loopIteration, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("SaveCheckpoint", ActivityKind.Internal);
        activity?.SetTag("pipeline.run.id", run.Id.ToString());
        activity?.SetTag("checkpoint.phase", phase.ToString());
        activity?.SetTag("checkpoint.iteration", loopIteration);
        activity?.SetTag("checkpoint.total_nodes", dag.Nodes.Count);

        try
        {
            var completedNodes = dag.Nodes.Where(n => n.Status == PipelineStatus.Completed).ToList();
            var failedNodes = dag.Nodes.Where(n => n.Status == PipelineStatus.Failed).ToList();
            var checkpoint = PipelineCheckpoint.Create(run.Id, phase.ToString(), completedNodes, failedNodes, loopIteration);
            await _store.SaveCheckpointAsync(checkpoint, ct);
            activity?.SetTag("checkpoint.completed_nodes", completedNodes.Count);
            activity?.SetTag("checkpoint.failed_nodes", failedNodes.Count);
            Log.Debug("Checkpoint saved: phase {Phase}, {Completed}/{Total} nodes", phase, completedNodes.Count, dag.Nodes.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save checkpoint for pipeline {PipelineId}", run.Id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        }
    }

    /// <summary>
    /// Stores advisory failure risk metadata on the pipeline run.
    /// This is purely informational — it never bypasses or modifies gate logic.
    /// Risks are flattened as "risk_{level}" keys with JSON-like values.
    /// </summary>
    private async Task StoreRiskMetadataAsync(PipelineDag dag, PipelineRun run, CancellationToken ct)
    {
        try
        {
            run.AddMetadata("adaptive_risk_checked_at", DateTime.UtcNow.ToString("O"));
            var risks = await _adaptiveGates.PredictFailureAsync(run, dag, ct);
            for (int i = 0; i < risks.Count; i++)
            {
                var r = risks[i];
                run.AddMetadata($"adaptive_risk_{i}", $"level={r.RiskLevel};score={r.RiskScore};{r.Reason}");
            }
            run.AddMetadata("adaptive_risk_count", risks.Count.ToString());
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to store advisory risk metadata (non-fatal)");
        }
    }

    private static void IncrementActivePipelines() => Interlocked.Increment(ref _activePipelineCount);
    private static void DecrementActivePipelines() => Interlocked.Decrement(ref _activePipelineCount);
}
