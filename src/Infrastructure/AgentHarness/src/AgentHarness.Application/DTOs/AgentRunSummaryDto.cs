namespace His.Hope.AgentHarness.Application.DTOs;

public record AgentRunSummaryDto
{
    public Guid AgentRunId { get; init; }
    public Guid PipelineRunId { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal? ConfidenceScore { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public double? DurationSeconds { get; init; }
    public string? ArtifactRef { get; init; }
}
