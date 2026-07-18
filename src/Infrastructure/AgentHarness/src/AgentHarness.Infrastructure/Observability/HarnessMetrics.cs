using System.Diagnostics.Metrics;

namespace His.Hope.AgentHarness.Infrastructure.Observability;

public static class HarnessMetrics
{
    private static readonly Meter Meter = new("His.Hope.AgentHarness", "1.0.0");

    // Counters
    public static readonly Counter<int> PipelineStartCount =
        Meter.CreateCounter<int>("pipeline.start.count", description: "Number of pipelines started");

    public static readonly Counter<int> PipelineCompleteCount =
        Meter.CreateCounter<int>("pipeline.complete.count", description: "Number of pipelines completed");

    public static readonly Counter<int> AgentDispatchCount =
        Meter.CreateCounter<int>("agent.dispatch.count", description: "Number of agent dispatches");

    public static readonly Counter<int> AgentRetryCount =
        Meter.CreateCounter<int>("agent.retry.count", description: "Number of agent retries");

    public static readonly Counter<int> EventPublishedCount =
        Meter.CreateCounter<int>("event.published.count", description: "Number of events published");

    // Histograms
    public static readonly Histogram<double> PipelineDuration =
        Meter.CreateHistogram<double>("pipeline.duration.seconds", unit: "s",
            description: "Duration of pipeline executions");

    public static readonly Histogram<double> AgentDuration =
        Meter.CreateHistogram<double>("agent.duration.seconds", unit: "s",
            description: "Duration of agent executions");

    // ObservableGauge — tracks active pipelines via Interlocked
    private static int _activePipelines;

    static HarnessMetrics()
    {
        Meter.CreateObservableGauge("pipeline.active", () => new[]
        {
            new Measurement<int>(Volatile.Read(ref _activePipelines))
        }, description: "Number of currently active pipelines");
    }

    public static int ActivePipelines
    {
        get => Volatile.Read(ref _activePipelines);
        set => Interlocked.Exchange(ref _activePipelines, value);
    }

    public static void IncrementActivePipelines() => Interlocked.Increment(ref _activePipelines);
    public static void DecrementActivePipelines() => Interlocked.Decrement(ref _activePipelines);
}
