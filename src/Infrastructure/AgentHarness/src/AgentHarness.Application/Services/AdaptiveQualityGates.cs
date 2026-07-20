using His.Hope.AgentHarness.Application.DTOs;
using His.Hope.AgentHarness.Application.Interfaces;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Application.Services;

/// <summary>
/// Adaptive quality gate service that provides advisory risk predictions
/// and threshold recommendations based on historical AIS scores and
/// quality gate pass rates.
///
/// <para>This service is purely advisory — it NEVER bypasses, auto-passes,
/// or skips quality gates. All risk metadata is stored on the pipeline run
/// via <see cref="PipelineRun.AddMetadata"/>.</para>
/// </summary>
public class AdaptiveQualityGates
{
    private readonly IAgentMetricsService _metrics;
    private readonly IStateStore _store;

    public AdaptiveQualityGates(IAgentMetricsService metrics, IStateStore store)
    {
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// Predicts failure risk for each agent/phase node in the DAG.
    /// Returns one <see cref="FailureRiskDto"/> per node.
    /// </summary>
    public async Task<IReadOnlyList<FailureRiskDto>> PredictFailureAsync(
        PipelineRun run,
        PipelineDag dag,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dag);

        if (dag.Nodes.Count == 0)
            return Array.Empty<FailureRiskDto>();

        // Fetch gates once to avoid repeated store calls
        var allGates = await _store.GetQualityGatesAsync(run.Id, ct);
        var results = new List<FailureRiskDto>(dag.Nodes.Count);

        foreach (var node in dag.Nodes)
        {
            AgentProfileDto profile;
            try
            {
                profile = await _metrics.GetAgentProfileAsync(node.AgentName, ct);
            }
            catch
            {
                profile = new AgentProfileDto { AgentName = node.AgentName };
            }

            profile ??= new AgentProfileDto { AgentName = node.AgentName };

            var agentGates = allGates.Where(g => GateAttributionHelper.GateBelongsToAgent(g.GateId, node.AgentName)).ToList();
            var evalAvgPassRate = await GetAgentEvalAvgPassRateAsync(node.AgentName, ct);

            var risk = CalculateNodeRisk(node, profile, agentGates, evalAvgPassRate);
            results.Add(risk);
        }

        return results.AsReadOnly();
    }

    /// <summary>
    /// Recommends a quality gate threshold (0.0 to 1.0) for the given agent.
    /// Higher thresholds mean stricter gates; lower thresholds mean more relaxed.
    /// Based on AIS score, historical gate pass rate, and eval history.
    /// </summary>
    public async Task<QualityGateRecommendationDto> RecommendThresholdsAsync(
        string agentName,
        CancellationToken ct = default)
    {
        var profile = await _metrics.GetAgentProfileAsync(agentName, ct);
        var evalAvgPassRate = await GetAgentEvalAvgPassRateAsync(agentName, ct);

        // Base threshold from AIS: low AIS → stricter (higher) threshold
        // Map AIS (0-100) to base threshold: AIS=0 → 0.9, AIS=100 → 0.1
        var baseThreshold = 0.9 - (profile.AisScore / 100.0) * 0.8;
        baseThreshold = Math.Clamp(baseThreshold, 0.1, 0.99);

        // Adjust for historical gate pass rate
        // Low pass rate → stricter threshold
        if (profile.QualityGatePassRate > 0 && profile.TotalRuns > 0)
        {
            var passRateFactor = 1.0 - profile.QualityGatePassRate; // 0 when perfect, 1 when terrible
            baseThreshold += passRateFactor * 0.15; // max +0.15 for poor pass rate
        }

        // Adjust for eval history: low eval pass rate → stricter threshold
        if (evalAvgPassRate.HasValue && evalAvgPassRate.Value > 0)
        {
            var evalFactor = 1.0 - evalAvgPassRate.Value; // 0 when perfect eval, 1 when terrible
            baseThreshold += evalFactor * 0.10; // max +0.10 for poor eval performance
        }

        baseThreshold = Math.Clamp(baseThreshold, 0.1, 0.99);

        return new QualityGateRecommendationDto
        {
            AgentName = agentName,
            RecommendedGateThreshold = Math.Round(baseThreshold, 4),
            AisScore = profile.AisScore,
            HistoricalPassRate = profile.QualityGatePassRate,
            LastUpdatedAt = DateTime.UtcNow
        };
    }

    private FailureRiskDto CalculateNodeRisk(
        PipelineNode node,
        AgentProfileDto profile,
        List<QualityGate> agentGates,
        double? evalAvgPassRate = null)
    {
        // No profile data: cautious default
        if (profile.TotalRuns == 0 || profile.AisScore <= 0)
        {
            return new FailureRiskDto
            {
                RiskScore = 0.75,
                RiskLevel = "High",
                Reason = $"Agent '{node.AgentName}' has no historical data; conservative risk estimate.",
                SuggestedModel = null
            };
        }

        // Base risk from AIS: low AIS → high risk
        var riskScore = 1.0 - (profile.AisScore / 100.0);

        // Adjust for task completion rate
        if (profile.TaskCompletionRate > 0)
        {
            riskScore *= (1.0 - profile.TaskCompletionRate * 0.3);
        }

        // Adjust for gate pass rate
        if (profile.QualityGatePassRate > 0)
        {
            riskScore *= (1.0 - profile.QualityGatePassRate * 0.2);
        }

        // Adjust for retry rate (higher retry rate = fewer retries needed = lower risk)
        if (profile.RetryRate > 0)
        {
            riskScore *= (1.0 - profile.RetryRate * 0.15);
        }

        // Adjust for eval history: higher eval pass@1 → lower risk
        if (evalAvgPassRate.HasValue && evalAvgPassRate.Value > 0)
        {
            riskScore *= (1.0 - evalAvgPassRate.Value * 0.10);
        }

        riskScore = Math.Clamp(riskScore, 0.01, 0.99);

        var riskLevel = riskScore switch
        {
            <= 0.25 => "Low",
            <= 0.50 => "Medium",
            <= 0.75 => "High",
            _ => "Critical"
        };

        var reasons = new List<string>();
        if (profile.AisScore < 40)
            reasons.Add($"low AIS ({profile.AisScore:F1})");
        if (profile.QualityGatePassRate < 0.5)
            reasons.Add($"poor gate pass rate ({profile.QualityGatePassRate:P1})");
        if (profile.TotalRuns < 5)
            reasons.Add($"limited history ({profile.TotalRuns} runs)");

        var reason = reasons.Count > 0
            ? $"Risk factors: {string.Join("; ", reasons)}."
            : $"Agent '{node.AgentName}' has adequate historical performance (AIS {profile.AisScore:F1}).";

        return new FailureRiskDto
        {
            RiskScore = Math.Round(riskScore, 4),
            RiskLevel = riskLevel,
            Reason = reason,
            SuggestedModel = null
        };
    }

    /// <summary>
    /// Computes the average pass@1 rate from eval history for the given agent.
    /// Uses existing <see cref="IStateStore"/> eval access — iterates all eval
    /// suites and their runs, filtering by agent name and completed status.
    /// Returns null when no eval history exists.
    /// </summary>
    private async Task<double?> GetAgentEvalAvgPassRateAsync(string agentName, CancellationToken ct)
    {
        try
        {
            var suites = await _store.GetEvalSuitesAsync(ct);
            if (suites.Count == 0) return null;

            double totalPassRate = 0;
            int completedCount = 0;

            foreach (var suite in suites)
            {
                var runs = await _store.GetEvalRunsAsync(suite.Id, ct);
                var completedRuns = runs
                    .Where(r => r.Status == EvalRunStatus.Completed
                        && r.TargetAgent.Equals(agentName, StringComparison.OrdinalIgnoreCase)
                        && r.PassAt1.HasValue)
                    .ToList();

                foreach (var run in completedRuns)
                {
                    totalPassRate += run.PassAt1!.Value;
                    completedCount++;
                }
            }

            return completedCount > 0 ? totalPassRate / completedCount : null;
        }
        catch
        {
            // Non-fatal: eval history is advisory only
            return null;
        }
    }
}
