namespace His.Hope.AgentHarness.Core.Events;

public class AgentCompleted
{
    public Guid AgentRunId { get; }
    public Guid PipelineRunId { get; }
    public string AgentName { get; }
    public decimal ConfidenceScore { get; }
    public string ArtifactRef { get; }
    public DateTime CompletedAt { get; }

    public AgentCompleted(Guid agentRunId, Guid pipelineRunId, string agentName, decimal confidenceScore, string artifactRef)
    {
        AgentRunId = agentRunId;
        PipelineRunId = pipelineRunId;
        AgentName = agentName;
        ConfidenceScore = confidenceScore;
        ArtifactRef = artifactRef;
        CompletedAt = DateTime.UtcNow;
    }
}
