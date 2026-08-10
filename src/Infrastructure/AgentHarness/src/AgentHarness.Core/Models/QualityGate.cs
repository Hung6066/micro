namespace His.Hope.AgentHarness.Core.Models;

public class QualityGate
{
    public Guid Id { get; private set; }
    public Guid PipelineRunId { get; private set; }
    public string GateId { get; private set; } = string.Empty;
    public string GateType { get; private set; } = string.Empty;
    public string GateName { get; private set; } = string.Empty;
    public bool Passed { get; private set; }
    public string? Details { get; private set; }
    public string? Output { get; private set; }
    public GateSeverity Severity { get; private set; }
    public DateTime EvaluatedAt { get; private set; }

    private QualityGate() { }

    public static QualityGate Create(Guid pipelineRunId, string gateId, string gateType, bool passed, string? details = null)
    {
        return new QualityGate
        {
            Id = Guid.NewGuid(),
            PipelineRunId = pipelineRunId,
            GateId = gateId,
            GateType = gateType,
            Passed = passed,
            Details = details,
            EvaluatedAt = DateTime.UtcNow
        };
    }

    public static QualityGate Create(Guid id, Guid pipelineRunId, string gateId, string gateName, GateSeverity severity)
    {
        return new QualityGate
        {
            Id = id,
            PipelineRunId = pipelineRunId,
            GateId = gateId,
            GateName = gateName,
            Severity = severity,
            Passed = true,
            EvaluatedAt = DateTime.UtcNow
        };
    }

    public void MarkFailed(string output)
    {
        Passed = false;
        Output = output;
        EvaluatedAt = DateTime.UtcNow;
    }
}
