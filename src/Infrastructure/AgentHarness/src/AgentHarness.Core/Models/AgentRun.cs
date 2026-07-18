namespace His.Hope.AgentHarness.Core.Models;

public enum AgentRunStatus { Pending, Running, Completed, Failed, Cancelled, TimedOut }
public enum CircuitState { Closed, Open, HalfOpen }

public class AgentRun
{
    public Guid Id { get; private set; }
    public Guid PipelineRunId { get; private set; }
    public string AgentName { get; private set; } = string.Empty;
    public string TaskDescription { get; private set; } = string.Empty;
    public AgentRunStatus Status { get; private set; }
    public int AttemptNumber { get; private set; } = 1;
    public decimal? ConfidenceScore { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? OutputArtifactRef { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; } = 3;
    public int TimeoutSeconds { get; private set; } = 600;
    public CircuitState CircuitState { get; private set; } = CircuitState.Closed;
    public DateTime CreatedAt { get; private set; }

    private AgentRun() { }

    public static AgentRun Create(Guid pipelineRunId, string agentName, string taskDescription, int maxRetries = 3, int timeoutSeconds = 600)
    {
        return new AgentRun
        {
            Id = Guid.NewGuid(),
            PipelineRunId = pipelineRunId,
            AgentName = agentName,
            TaskDescription = taskDescription,
            Status = AgentRunStatus.Pending,
            MaxRetries = maxRetries,
            TimeoutSeconds = timeoutSeconds,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Start() { Status = AgentRunStatus.Running; StartedAt = DateTime.UtcNow; }
    public void Complete(decimal confidenceScore, string artifactRef) { Status = AgentRunStatus.Completed; CompletedAt = DateTime.UtcNow; ConfidenceScore = confidenceScore; OutputArtifactRef = artifactRef; }
    public void Fail(string error) { Status = AgentRunStatus.Failed; CompletedAt = DateTime.UtcNow; ErrorMessage = error; RetryCount++; }
    public void Timeout() { Status = AgentRunStatus.TimedOut; CompletedAt = DateTime.UtcNow; ErrorMessage = "Agent execution timed out"; }
    public bool IsTerminal() => Status is AgentRunStatus.Completed or AgentRunStatus.Failed or AgentRunStatus.Cancelled or AgentRunStatus.TimedOut;
    public bool CanRetry() => RetryCount < MaxRetries;
    public void OpenCircuit() => CircuitState = CircuitState.Open;
    public void HalfOpenCircuit() => CircuitState = CircuitState.HalfOpen;
    public void CloseCircuit() => CircuitState = CircuitState.Closed;
}
