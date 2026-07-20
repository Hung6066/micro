namespace His.Hope.AgentHarness.Application.DTOs;

public record EvalRunDto
{
    public Guid EvalRunId { get; init; }
    public string EvalSuiteName { get; init; } = string.Empty;
    public string TargetAgent { get; init; } = string.Empty;
    public string? TargetModel { get; init; }
    public double? PassAt1 { get; init; }
    public double? PassAtK { get; init; }
    public int? JudgeScore { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}
