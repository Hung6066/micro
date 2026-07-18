using System.Text.Json;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Mcp.Tools;

/// <summary>
/// Returns all agent runs currently in Running status across all pipelines.
/// External agents (OpenCode) poll this to discover work assignments.
/// </summary>
public class GetPendingTasksTool
{
    private readonly IStateStore _store;

    public GetPendingTasksTool(IStateStore store) => _store = store;

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        // Optional filter by pipeline_run_id
        var pipelineFilter = parameters.GetValueOrDefault("pipeline_run_id")?.ToString();

        var pending = await _store.GetPendingAgentRunsAsync();

        if (!string.IsNullOrEmpty(pipelineFilter) && Guid.TryParse(pipelineFilter, out var pipelineId))
        {
            pending = pending.Where(r => r.PipelineRunId == pipelineId).ToList();
        }

        var result = pending.Select(r => new
        {
            agent_run_id = r.Id.ToString(),
            pipeline_run_id = r.PipelineRunId.ToString(),
            agent_name = r.AgentName,
            task_description = r.TaskDescription,
            started_at = r.StartedAt,
            timeout_seconds = r.TimeoutSeconds,
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            count = result.Count,
            tasks = result
        });
    }
}
