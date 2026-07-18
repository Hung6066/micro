using System.Threading;

namespace His.Hope.AgentHarness.Application.Services;

/// <summary>
/// Thread-safe backpressure controller that enforces concurrency limits
/// for pipelines and agent dispatches using atomic counters.
/// </summary>
public class BackpressureController
{
    private const int MaxPipelineQueue = 10;
    private const int MaxAgentQueue = 20;

    private int _activePipelines;
    private int _activeAgents;

    /// <summary>
    /// Ensures the pipeline queue has capacity. Throws <see cref="InvalidOperationException"/>
    /// with "HTTP 429" when the maximum number of pipelines is already queued.
    /// On success, increments the active pipeline count.
    /// </summary>
    public void EnsureCapacity()
    {
        var current = Interlocked.Increment(ref _activePipelines);
        if (current > MaxPipelineQueue)
        {
            Interlocked.Decrement(ref _activePipelines);
            throw new InvalidOperationException("HTTP 429: Too many pipelines in queue. Maximum allowed: " + MaxPipelineQueue);
        }
    }

    /// <summary>
    /// Releases a pipeline slot by decrementing the active pipeline counter.
    /// Called by PipelineEngine in its finally block.
    /// </summary>
    public void ReleasePipeline() => Interlocked.Decrement(ref _activePipelines);

    /// <summary>
    /// Tracks an agent dispatch. Returns true if under the agent queue limit,
    /// false if the limit is exceeded (in which case the agent count is not incremented).
    /// </summary>
    public bool TryTrackAgent()
    {
        var current = Interlocked.Increment(ref _activeAgents);
        if (current > MaxAgentQueue)
        {
            Interlocked.Decrement(ref _activeAgents);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Releases an agent slot by decrementing the active agent counter.
    /// </summary>
    public void ReleaseAgent() => Interlocked.Decrement(ref _activeAgents);

    /// <summary>Gets the current number of active pipelines.</summary>
    public int ActivePipelineCount => Volatile.Read(ref _activePipelines);

    /// <summary>Gets the current number of active agent dispatches.</summary>
    public int ActiveAgentCount => Volatile.Read(ref _activeAgents);
}
