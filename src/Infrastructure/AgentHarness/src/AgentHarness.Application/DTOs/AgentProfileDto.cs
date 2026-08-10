namespace His.Hope.AgentHarness.Application.DTOs;

public record AgentProfileDto
{
    public string AgentName { get; init; } = string.Empty;
    public double AisScore { get; init; }
    public double TaskCompletionRate { get; init; }
    public double QualityGatePassRate { get; init; }
    public double RetryRate { get; init; }
    public double ConfidenceAccuracy { get; init; }
    public double LearningEffectiveness { get; init; }
    public double AverageJudgeScore { get; init; }
    public int TotalRuns { get; init; }
    public int SuccessfulRuns { get; init; }
    public IReadOnlyList<AgentRunSummaryDto> RecentRuns { get; init; } = Array.Empty<AgentRunSummaryDto>();
}
