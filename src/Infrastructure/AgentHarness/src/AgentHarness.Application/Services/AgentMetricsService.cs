using System.Diagnostics.Metrics;
using His.Hope.AgentHarness.Application.DTOs;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Application.Services;

public class AgentMetricsService
{
    private static readonly Meter Meter = new("His.Hope.AgentHarness", "1.0.0");
    private static readonly Counter<int> ProfileQueryCount =
        Meter.CreateCounter<int>("agent.profile.query.count", description: "Number of agent profile queries");
    private static readonly Histogram<double> AgentAisScore =
        Meter.CreateHistogram<double>("agent.ais.score", unit: "score",
            description: "Agent Intelligence Score (0-100)");

    private readonly IStateStore _store;
    private const int MaxRecentRuns = 20;

    public AgentMetricsService(IStateStore store)
    {
        _store = store;
    }

    public async Task<AgentProfileDto> GetAgentProfileAsync(string agentName, CancellationToken ct = default)
    {
        ProfileQueryCount.Add(1);

        var runs = await _store.GetAgentRunsByAgentNameAsync(agentName, ct);
        var memories = await _store.GetMemoryEntriesAsync(ct);
        var agentMemories = memories
            .Where(m => m.AgentName.Equals(agentName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        int totalRuns = runs.Count;
        int successfulRuns = runs.Count(r => r.Status is AgentRunStatus.Completed);

        // Task Completion Rate
        double taskCompletionRate = totalRuns > 0 ? (double)successfulRuns / totalRuns : 0.0;

        // Retry Rate (higher = better; fewer retries per run)
        int totalRetries = runs.Sum(r => r.RetryCount);
        double avgMaxRetries = totalRuns > 0 ? runs.Average(r => r.MaxRetries) : 1.0;
        double retryRate = totalRuns > 0
            ? 1.0 - Math.Min(totalRetries / (totalRuns * avgMaxRetries), 1.0)
            : 0.0;

        // Quality Gate Pass Rate (aggregated across all pipeline runs for this agent)
        int totalGates = 0;
        int passedGates = 0;
        foreach (var pipelineId in runs.Select(r => r.PipelineRunId).Distinct())
        {
            var gates = await _store.GetQualityGatesAsync(pipelineId, ct);
            totalGates += gates.Count;
            passedGates += gates.Count(g => g.Passed);
        }
        double qualityGatePassRate = totalGates > 0 ? (double)passedGates / totalGates : 0.0;

        // Confidence Accuracy
        double confidenceAccuracy = 0;
        if (totalRuns > 0)
        {
            double accSum = runs
                .Where(r => r.Status == AgentRunStatus.Completed && r.ConfidenceScore.HasValue)
                .Sum(r => (double)r.ConfidenceScore!.Value);
            confidenceAccuracy = accSum / totalRuns;
        }

        // Average Judge Score (avg confidence of completed runs as proxy)
        var completedWithConfidence = runs
            .Where(r => r.Status == AgentRunStatus.Completed && r.ConfidenceScore.HasValue)
            .ToList();
        double averageJudgeScore = completedWithConfidence.Count > 0
            ? completedWithConfidence.Average(r => (double)r.ConfidenceScore!)
            : 0.0;

        // Learning Effectiveness (from memory entries)
        double learningEffectiveness = agentMemories.Count > 0
            ? (double)agentMemories.Count(m => m.Success) / agentMemories.Count
            : 0.0;

        // Weighted AIS Score (0-100)
        double aisScore = (
            taskCompletionRate * 0.25 +
            qualityGatePassRate * 0.20 +
            retryRate * 0.15 +
            confidenceAccuracy * 0.15 +
            learningEffectiveness * 0.10 +
            averageJudgeScore * 0.15
        ) * 100;

        // Recent runs (newest-first, bounded)
        var recentRuns = runs
            .OrderByDescending(r => r.CompletedAt ?? r.CreatedAt)
            .Take(MaxRecentRuns)
            .Select(r => new AgentRunSummaryDto
            {
                AgentRunId = r.Id,
                PipelineRunId = r.PipelineRunId,
                Status = r.Status.ToString(),
                ConfidenceScore = r.ConfidenceScore,
                StartedAt = r.StartedAt,
                CompletedAt = r.CompletedAt,
                DurationSeconds = r.StartedAt.HasValue && r.CompletedAt.HasValue
                    ? (r.CompletedAt.Value - r.StartedAt.Value).TotalSeconds
                    : null,
                ArtifactRef = r.OutputArtifactRef
            })
            .ToList();

        // Record AIS score metric
        AgentAisScore.Record(aisScore);

        return new AgentProfileDto
        {
            AgentName = agentName,
            AisScore = Math.Round(aisScore, 2),
            TaskCompletionRate = Math.Round(taskCompletionRate, 4),
            QualityGatePassRate = Math.Round(qualityGatePassRate, 4),
            RetryRate = Math.Round(retryRate, 4),
            ConfidenceAccuracy = Math.Round(confidenceAccuracy, 4),
            LearningEffectiveness = Math.Round(learningEffectiveness, 4),
            AverageJudgeScore = Math.Round(averageJudgeScore, 4),
            TotalRuns = totalRuns,
            SuccessfulRuns = successfulRuns,
            RecentRuns = recentRuns
        };
    }
}
