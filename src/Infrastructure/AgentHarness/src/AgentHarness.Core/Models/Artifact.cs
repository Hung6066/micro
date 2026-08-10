namespace His.Hope.AgentHarness.Core.Models;

public class Artifact
{
    public Guid Id { get; private set; }
    public Guid PipelineRunId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public byte[]? Content { get; private set; } // Inline artifact data
    public DateTime CreatedAt { get; private set; }

    private Artifact() { }

    public static Artifact Create(Guid pipelineRunId, string name, string contentType, string storagePath, long sizeBytes, byte[]? content = null)
    {
        return new Artifact
        {
            Id = Guid.NewGuid(),
            PipelineRunId = pipelineRunId,
            Name = name,
            ContentType = contentType,
            StoragePath = storagePath,
            SizeBytes = sizeBytes,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
    }
}
