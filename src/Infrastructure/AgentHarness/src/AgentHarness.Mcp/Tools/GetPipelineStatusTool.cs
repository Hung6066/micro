using System.Text.Json;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Mcp.Tools;

/// <summary>
/// Enhanced pipeline status — returns pipeline run + all agent runs + quality gates.
/// </summary>
public class GetPipelineStatusTool
{
    private readonly IStateStore _store;

    public GetPipelineStatusTool(IStateStore store) => _store = store;

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var pipelineRunIdStr = parameters.GetValueOrDefault("pipeline_run_id")?.ToString()
            ?? throw new ArgumentException("'pipeline_run_id' is required.");

        if (!Guid.TryParse(pipelineRunIdStr, out var pipelineRunId))
            throw new ArgumentException("'pipeline_run_id' must be a valid GUID.");

        var run = await _store.GetPipelineRunAsync(pipelineRunId);
        if (run == null)
            throw new InvalidOperationException($"Pipeline run {pipelineRunId} not found.");

        var agentRuns = await _store.GetAgentRunsAsync(pipelineRunId);
        var gates = await _store.GetQualityGatesAsync(pipelineRunId);
        var childRuns = await _store.GetChildPipelineRunsAsync(pipelineRunId);

        var result = new
        {
            pipeline_run_id = run.Id.ToString(),
            parent_pipeline_run_id = run.ParentPipelineRunId?.ToString(),
            workflow_id = run.WorkflowId,
            status = run.Status.ToString(),
            triggered_by = run.TriggeredBy,
            started_at = run.StartedAt,
            completed_at = run.CompletedAt,
            agent_runs = agentRuns.Select(a => new
            {
                id = a.Id.ToString(),
                agent_name = a.AgentName,
                task = a.TaskDescription,
                status = a.Status.ToString(),
                started_at = a.StartedAt,
                completed_at = a.CompletedAt,
                confidence = a.ConfidenceScore,
                error = a.ErrorMessage,
                artifact = a.OutputArtifactRef,
            }).ToList(),
            quality_gates = gates.Select(g => new
            {
                id = g.Id.ToString(),
                gate = g.GateType ?? g.GateId,
                passed = g.Passed,
                details = g.Details,
            }).ToList(),
            metadata = run.Metadata,
            child_pipelines = childRuns.Select(c => new
            {
                pipeline_run_id = c.Id.ToString(),
                workflow_id = c.WorkflowId,
                status = c.Status.ToString(),
                started_at = c.StartedAt,
                completed_at = c.CompletedAt
            }).ToList(),
        };

        return JsonSerializer.Serialize(result);
    }
}
