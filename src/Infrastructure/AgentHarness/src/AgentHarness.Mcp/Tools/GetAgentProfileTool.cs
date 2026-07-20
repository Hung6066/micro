using System.Text.Json;
using His.Hope.AgentHarness.Application.DTOs;
using His.Hope.AgentHarness.Application.Services;

namespace His.Hope.AgentHarness.Mcp.Tools;

public class GetAgentProfileTool
{
    private readonly AgentMetricsService _service;

    public GetAgentProfileTool(AgentMetricsService service) => _service = service;

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var agentName = parameters.GetValueOrDefault("agent_name")?.ToString()
            ?? throw new ArgumentException("'agent_name' is required.");

        var profile = await _service.GetAgentProfileAsync(agentName);

        return JsonSerializer.Serialize(new
        {
            agent_name = profile.AgentName,
            ais_score = profile.AisScore,
            task_completion_rate = profile.TaskCompletionRate,
            quality_gate_pass_rate = profile.QualityGatePassRate,
            retry_rate = profile.RetryRate,
            confidence_accuracy = profile.ConfidenceAccuracy,
            learning_effectiveness = profile.LearningEffectiveness,
            average_judge_score = profile.AverageJudgeScore,
            total_runs = profile.TotalRuns,
            successful_runs = profile.SuccessfulRuns,
            recent_runs = profile.RecentRuns.Select(r => new
            {
                agent_run_id = r.AgentRunId.ToString(),
                pipeline_run_id = r.PipelineRunId.ToString(),
                status = r.Status,
                confidence_score = r.ConfidenceScore,
                started_at = r.StartedAt,
                completed_at = r.CompletedAt,
                duration_seconds = r.DurationSeconds,
                artifact_ref = r.ArtifactRef
            })
        });
    }
}
