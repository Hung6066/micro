namespace His.Hope.AgentHarness.Core.Events;

public class AgentFailed
{
    public Guid AgentRunId { get; }
    public Guid PipelineRunId { get; }
    public string AgentName { get; }
    public string ErrorMessage { get; }
    public int RetryCount { get; }
    public DateTime FailedAt { get; }

    public AgentFailed(Guid agentRunId, Guid pipelineRunId, string agentName, string errorMessage, int retryCount)
    {
        AgentRunId = agentRunId;
        PipelineRunId = pipelineRunId;
        AgentName = agentName;
        ErrorMessage = errorMessage;
        RetryCount = retryCount;
        FailedAt = DateTime.UtcNow;
    }
}
