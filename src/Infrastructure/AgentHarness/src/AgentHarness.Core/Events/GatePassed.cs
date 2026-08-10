namespace His.Hope.AgentHarness.Core.Events;

public class GatePassed
{
    public Guid PipelineRunId { get; }
    public string GateId { get; }
    public string GateType { get; }
    public DateTime PassedAt { get; }

    public GatePassed(Guid pipelineRunId, string gateId, string gateType)
    {
        PipelineRunId = pipelineRunId;
        GateId = gateId;
        GateType = gateType;
        PassedAt = DateTime.UtcNow;
    }
}
