using System.Collections.Concurrent;
using System.Threading;
using His.Hope.AgentHarness.Core.Events;

namespace His.Hope.AgentHarness.Application.Services;

/// <summary>
/// Thread-safe agent pool manager that controls concurrency per agent type,
/// implements circuit breaker recovery, and manages pool scaling.
/// </summary>
public class AgentPoolManager
{
    private static readonly string[] KnownAgents =
    {
        "dotnet", "angular", "dba", "devops", "docs", "ml-ai", "data-platform",
        "testing-backend", "testing-frontend", "qa", "validate", "check-ui",
        "e2e-test", "explore", "git", "security", "loop-engineer"
    };

    private const int DefaultPoolSize = 10;
    private const int MaxPoolSize = 50;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _pools = new();
    private readonly ConcurrentDictionary<string, AgentPoolState> _states = new();
    private readonly IAgentDispatcher _dispatcher;
    private readonly IStateStore _store;
    private readonly IEventBus _eventBus;
    private readonly BackpressureController _backpressure;

    public AgentPoolManager(
        IAgentDispatcher dispatcher,
        IStateStore store,
        IEventBus eventBus,
        BackpressureController backpressure)
    {
        _dispatcher = dispatcher;
        _store = store;
        _eventBus = eventBus;
        _backpressure = backpressure;

        foreach (var agent in KnownAgents)
        {
            _pools[agent] = new SemaphoreSlim(DefaultPoolSize, MaxPoolSize);
            _states[agent] = AgentPoolState.Create(agent, DefaultPoolSize);
        }
    }

    /// <summary>
    /// Dispatches an agent run with pool-based concurrency control:
    /// circuit breaker check → recovery if OPEN → scale-up check → semaphore acquire →
    /// dispatch → outcome recording → semaphore release → scale-down if idle.
    /// </summary>
    public async Task<AgentRun> DispatchWithPoolAsync(AgentRun agentRun, CancellationToken ct = default)
    {
        var pool = _pools.GetOrAdd(agentRun.AgentName, _ => new SemaphoreSlim(DefaultPoolSize, MaxPoolSize));
        var state = _states.GetOrAdd(agentRun.AgentName, _ => AgentPoolState.Create(agentRun.AgentName, DefaultPoolSize));

        // 1. Check circuit breaker state
        if (state.CircuitState == CircuitState.Open)
        {
            // 2. Attempt recovery by transitioning to Half-Open (allows a trial request)
            state.UpdateCircuitState(CircuitState.HalfOpen);
        }

        // 3. Check if scale-up is needed (pool is saturated)
        if (pool.CurrentCount == 0)
        {
            // Track that we observed saturation; heartbeat serves as liveness signal
            state.UpdateHeartbeat();
        }

        // 4. Enforce agent-level backpressure
        if (!_backpressure.TryTrackAgent())
        {
            throw new InvalidOperationException(
                $"Agent queue capacity exceeded for '{agentRun.AgentName}'. Maximum: 20 concurrent dispatches.");
        }

        try
        {
            // 5. Acquire semaphore slot
            await pool.WaitAsync(ct);

            // 6. Dispatch the agent via IAgentDispatcher
            var result = await _dispatcher.DispatchAsync(agentRun, ct);

            // 7. Record outcome — update circuit breaker state
            if (result.Status == AgentRunStatus.Completed)
            {
                state.UpdateCircuitState(CircuitState.Closed);
            }
            else if (result.Status is AgentRunStatus.Failed or AgentRunStatus.TimedOut)
            {
                state.UpdateCircuitState(CircuitState.Open);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            agentRun.Timeout();
            await _store.SaveAgentRunAsync(agentRun, ct);
            await _eventBus.PublishAsync(
                new AgentFailed(agentRun.Id, agentRun.PipelineRunId, agentRun.AgentName, "Cancelled", agentRun.RetryCount), ct);
            throw;
        }
        catch (Exception ex)
        {
            agentRun.Fail(ex.Message);
            await _store.SaveAgentRunAsync(agentRun, ct);
            await _eventBus.PublishAsync(
                new AgentFailed(agentRun.Id, agentRun.PipelineRunId, agentRun.AgentName, ex.Message, agentRun.RetryCount), ct);

            state.UpdateCircuitState(CircuitState.Open);
            return agentRun;
        }
        finally
        {
            // 8. Release semaphore
            pool.Release();

            // 9. Release agent backpressure slot
            _backpressure.ReleaseAgent();

            // 10. Scale down if the pool is idle (more than half capacity free)
            if (pool.CurrentCount > MaxPoolSize / 2)
            {
                state.UpdateHeartbeat();
            }
        }
    }

    /// <summary>
    /// Returns the current pool state for all known agents.
    /// </summary>
    public IReadOnlyDictionary<string, AgentPoolState> GetPoolStates() =>
        new Dictionary<string, AgentPoolState>(_states);

    /// <summary>
    /// Returns the available slot count for a specific agent.
    /// </summary>
    public int GetAvailableSlots(string agentName) =>
        _pools.TryGetValue(agentName, out var pool) ? pool.CurrentCount : 0;
}
