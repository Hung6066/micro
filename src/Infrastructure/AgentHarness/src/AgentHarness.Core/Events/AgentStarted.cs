namespace His.Hope.AgentHarness.Core.Events;

public class AgentStarted
{
    public Guid AgentRunId { get; }
    public Guid PipelineRunId { get; }
    public string AgentName { get; }
    public string TaskDescription { get; }
    public DateTime StartedAt { get; }

    public AgentStarted(Guid agentRunId, Guid pipelineRunId, string agentName, string taskDescription)
    {
        AgentRunId = agentRunId;
        PipelineRunId = pipelineRunId;
        AgentName = agentName;
        TaskDescription = taskDescription;
        StartedAt = DateTime.UtcNow;
    }
}
