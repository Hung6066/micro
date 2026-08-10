namespace His.Hope.AgentHarness.Core.Interfaces;

public interface IAgentMetricsRecorder
{
    void RecordProfileQuery();
    void RecordAisScore(double aisScore);
}
