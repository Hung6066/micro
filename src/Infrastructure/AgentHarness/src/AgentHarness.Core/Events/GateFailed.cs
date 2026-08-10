namespace His.Hope.AgentHarness.Core.Events;

public class GateFailed
{
    public Guid PipelineRunId { get; }
    public string GateId { get; }
    public string GateType { get; }
    public string Details { get; }
    public DateTime FailedAt { get; }

    public GateFailed(Guid pipelineRunId, string gateId, string gateType, string details)
    {
        PipelineRunId = pipelineRunId;
        GateId = gateId;
        GateType = gateType;
        Details = details;
        FailedAt = DateTime.UtcNow;
    }
}
