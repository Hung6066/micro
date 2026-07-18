namespace His.Hope.AgentHarness.Core.Events;

public class ArtifactReady
{
    public Guid ArtifactId { get; }
    public Guid PipelineRunId { get; }
    public string AgentName { get; }
    public string StoragePath { get; }
    public DateTime CreatedAt { get; }

    public ArtifactReady(Guid artifactId, Guid pipelineRunId, string agentName, string storagePath)
    {
        ArtifactId = artifactId;
        PipelineRunId = pipelineRunId;
        AgentName = agentName;
        StoragePath = storagePath;
        CreatedAt = DateTime.UtcNow;
    }
}
