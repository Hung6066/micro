using His.Hope.AgentHarness.Core.ValueObjects;

namespace His.Hope.AgentHarness.Application.Services;

/// <summary>
/// Aggregates confidence signals from agent runs, quality gates, and test results
/// to produce a <see cref="PipelineConfidence"/> score that drives the recommended action
/// (e.g., auto-commit, PR with light review, PR with full review, or escalate).
/// </summary>
public class PipelineConfidenceTracker
{
    /// <summary>
    /// Computes the overall pipeline confidence from its constituent signals.
    /// </summary>
    public PipelineConfidence Aggregate(
        PipelineRun run,
        List<AgentRun> agentRuns,
        List<QualityGate> gates,
        int loopCount)
    {
        var scores = new List<(decimal score, decimal weight)>();

        // Implement agents (weight: 0.4)
        var implRuns = agentRuns.Where(a => a.ConfidenceScore.HasValue).ToList();
        if (implRuns.Any())
        {
            var avgConfidence = implRuns.Average(a => a.ConfidenceScore!.Value);
            scores.Add((avgConfidence, 0.4m));
        }

        // Gates (weight: 0.3)
        if (gates.Any())
        {
            var passRate = (decimal)gates.Count(g => g.Passed) / gates.Count;
            scores.Add((passRate, 0.3m));
        }

        // Test results (weight: 0.2)
        var testResults = agentRuns.Count(
            a => a.AgentName.Contains("test", StringComparison.OrdinalIgnoreCase)
              && a.Status == AgentRunStatus.Completed);
        var totalTests = agentRuns.Count(
            a => a.AgentName.Contains("test", StringComparison.OrdinalIgnoreCase));
        if (totalTests > 0)
            scores.Add(((decimal)testResults / totalTests, 0.2m));

        // Loop penalty (weight: 0.1)
        var loopPenalty = Math.Max(0m, 1.0m - (loopCount * 0.15m));
        scores.Add((loopPenalty, 0.1m));

        return new PipelineConfidence(
            ConfidenceScore.FromWeightedSignals(scores.ToArray()));
    }
}

/// <summary>
/// Represents the aggregated confidence of a pipeline run and the recommended action.
/// </summary>
public class PipelineConfidence
{
    public ConfidenceScore Score { get; }

    /// <summary>
    /// Maps the confidence score to an action recommendation.
    /// </summary>
    public string RecommendedAction => Score.Value switch
    {
        > 0.9m => "auto-commit",
        >= 0.7m => "create-pr-light-review",
        >= 0.5m => "create-pr-full-review",
        _ => "escalate"
    };

    public PipelineConfidence(ConfidenceScore score) => Score = score;
}
