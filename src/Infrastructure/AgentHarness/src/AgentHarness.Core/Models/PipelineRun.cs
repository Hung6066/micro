namespace His.Hope.AgentHarness.Core.Models;

public enum PipelineStatus { Pending, Running, Completed, Failed, Cancelled }

public class PipelineRun
{
    public Guid Id { get; private set; }
    public Guid? ParentPipelineRunId { get; private set; }
    public string WorkflowId { get; private set; } = string.Empty;
    public PipelineStatus Status { get; private set; }
    public PipelineDag? DagDefinition { get; private set; }
    public Dictionary<string, string> Parameters { get; private set; } = new();
    public string TriggeredBy { get; private set; } = string.Empty;
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? TimeoutAt { get; private set; }
    public Dictionary<string, string> Metadata { get; private set; } = new();
    public DateTime CreatedAt { get; private set; }

    private PipelineRun() { }

    public static PipelineRun Create(string workflowId, Dictionary<string, string> parameters, string triggeredBy)
    {
        return new PipelineRun
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            Status = PipelineStatus.Pending,
            Parameters = parameters,
            TriggeredBy = triggeredBy,
            Metadata = new(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public static PipelineRun CreateChild(string workflowId, Dictionary<string, string> parameters, string triggeredBy, Guid parentPipelineRunId)
    {
        var run = Create(workflowId, parameters, triggeredBy);
        run.SetParent(parentPipelineRunId);
        return run;
    }

    public void SetParent(Guid parentPipelineRunId) => ParentPipelineRunId = parentPipelineRunId;

    public void TransitionTo(PipelineStatus newStatus)
    {
        if (Status == PipelineStatus.Cancelled)
            throw new InvalidOperationException("Cannot transition a cancelled pipeline.");
        Status = newStatus;
        if (newStatus == PipelineStatus.Running && StartedAt == null)
            StartedAt = DateTime.UtcNow;
        if (newStatus is PipelineStatus.Completed or PipelineStatus.Failed or PipelineStatus.Cancelled)
            CompletedAt = DateTime.UtcNow;
    }

    public void SetTimeout(TimeSpan timeout) => TimeoutAt = DateTime.UtcNow.Add(timeout);
    public bool IsTimedOut() => TimeoutAt.HasValue && DateTime.UtcNow > TimeoutAt.Value;
    public void SetDag(PipelineDag dag) => DagDefinition = dag;
    public void AddMetadata(string key, string value) => Metadata[key] = value;
}
