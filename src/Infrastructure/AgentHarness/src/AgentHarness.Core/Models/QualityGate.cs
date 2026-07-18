namespace His.Hope.AgentHarness.Core.Models;

public class QualityGate
{
    public Guid Id { get; private set; }
    public Guid PipelineRunId { get; private set; }
    public string GateId { get; private set; } = string.Empty;
    public string GateType { get; private set; } = string.Empty;
    public bool Passed { get; private set; }
    public string? Details { get; private set; }
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
}
