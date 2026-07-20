namespace His.Hope.AgentHarness.Application.DTOs;

public record InstinctOptimizationResultDto
{
    public int BoostedCount { get; init; }
    public int DecayedCount { get; init; }
    public int MergedCount { get; init; }
    public int RecordedCount { get; init; }
    public DateTime UpdatedAt { get; init; }
}
