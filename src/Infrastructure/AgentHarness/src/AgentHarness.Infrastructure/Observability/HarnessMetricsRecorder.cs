using His.Hope.AgentHarness.Core.Interfaces;

namespace His.Hope.AgentHarness.Infrastructure.Observability;

public sealed class HarnessMetricsRecorder : IAgentMetricsRecorder
{
    public void RecordProfileQuery() => HarnessMetrics.ProfileQueryCount.Add(1);

    public void RecordAisScore(double aisScore) => HarnessMetrics.AgentAisScore.Record(aisScore);
}
