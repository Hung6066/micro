using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
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

    public PipelineEngine(
        IAgentDispatcher dispatcher,
        IStateStore store,
        IEventBus eventBus,
        AgentPoolManager poolManager,
        BackpressureController backpressure)
    {
        _dispatcher = dispatcher;
        _store = store;
        _eventBus = eventBus;
        _poolManager = poolManager;
        _backpressure = backpressure;
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
            // 6. Execute DAG phases in strict order
            foreach (var phase in PhaseOrder)
            {
                // Check cancellation before starting a new phase
                cts.Token.ThrowIfCancellationRequested();

                // Check timeout before starting a new phase
                if (run.IsTimedOut())
                {
                    run.TransitionTo(PipelineStatus.Failed);
                    run.AddMetadata("timeout", "Pipeline exceeded its configured timeout before phase " + phase);
                    break;
                }

                var nodes = dag.GetPhaseNodes(phase).ToList();
                if (nodes.Count == 0)
                    continue;

                // Execute all nodes in this phase in parallel
                var tasks = nodes.Select(node => ExecuteNodeAsync(run, node, cts.Token));
                var results = await Task.WhenAll(tasks);

                // Check timeout after phase completes
                if (run.IsTimedOut())
                {
                    run.TransitionTo(PipelineStatus.Failed);
                    run.AddMetadata("timeout", "Pipeline exceeded its configured timeout after phase " + phase);
                    break;
                }

                // Check for node failures — if any node failed, mark pipeline failed
                if (results.Any(r => r.Status is AgentRunStatus.Failed or AgentRunStatus.TimedOut))
                {
                    run.TransitionTo(PipelineStatus.Failed);
                    var failedNode = nodes.FirstOrDefault(n => n.Status is PipelineStatus.Failed);
                    run.AddMetadata("failurePhase", phase.ToString());
                    run.AddMetadata("failedAgent", failedNode?.AgentName ?? "unknown");
                    break;
                }
            }

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

    /// <summary>
    /// Creates an <see cref="AgentRun"/> for the given pipeline node and dispatches it
    /// through the <see cref="AgentPoolManager"/>.
    /// </summary>
    private async Task<AgentRun> ExecuteNodeAsync(PipelineRun run, PipelineNode node, CancellationToken ct)
    {
        var agentRun = AgentRun.Create(
            run.Id,
            node.AgentName,
            $"Executing {node.AgentName} for phase {node.Phase}",
            maxRetries: 3);

        node.StartedAt = DateTime.UtcNow;

        var result = await _poolManager.DispatchWithPoolAsync(agentRun, ct);

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

        return result;
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
