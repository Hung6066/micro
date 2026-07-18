using System.Text.Json;
using System.Text.Json.Nodes;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Mcp.Tools;

public class TimelineTool
{
    private readonly IStateStore _store;

    public TimelineTool(IStateStore store) => _store = store;

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
        var checkpoints = new List<PipelineCheckpoint>();
        var latestCheckpoint = await _store.GetLatestCheckpointAsync(pipelineRunId);
        if (latestCheckpoint != null) checkpoints.Add(latestCheckpoint);

        var events = new List<(DateTime time, JsonObject obj)>();

        // Phase start from pipeline run
        events.Add((run.StartedAt ?? run.CreatedAt, new JsonObject
        {
            ["time"] = (run.StartedAt ?? run.CreatedAt).ToString("O"),
            ["type"] = "phase_start",
            ["agent"] = null,
            ["phase"] = run.WorkflowId,
            ["duration_ms"] = 0
        }));

        // Agent run events
        foreach (var agent in agentRuns.OrderBy(a => a.StartedAt ?? a.CreatedAt))
        {
            if (agent.StartedAt.HasValue)
            {
                events.Add((agent.StartedAt.Value, new JsonObject
                {
                    ["time"] = agent.StartedAt.Value.ToString("O"),
                    ["type"] = "agent_start",
                    ["agent"] = agent.AgentName,
                    ["phase"] = null,
                    ["duration_ms"] = 0
                }));
            }

            if (agent.CompletedAt.HasValue)
            {
                var duration = agent.StartedAt.HasValue
                    ? (long)(agent.CompletedAt.Value - agent.StartedAt.Value).TotalMilliseconds
                    : 0;

                events.Add((agent.CompletedAt.Value, new JsonObject
                {
                    ["time"] = agent.CompletedAt.Value.ToString("O"),
                    ["type"] = "agent_end",
                    ["agent"] = agent.AgentName,
                    ["phase"] = null,
                    ["duration_ms"] = duration
                }));
            }
        }

        // Checkpoint events
        foreach (var cp in checkpoints)
        {
            events.Add((cp.CreatedAt, new JsonObject
            {
                ["time"] = cp.CreatedAt.ToString("O"),
                ["type"] = "phase_end",
                ["agent"] = null,
                ["phase"] = cp.Phase,
                ["duration_ms"] = cp.LoopIteration > 0
                    ? (long)(cp.CreatedAt - run.CreatedAt).TotalMilliseconds
                    : 0
            }));
        }

        // Sort chronologically and extract sorted objects
        var sortedEvents = events
            .OrderBy(e => e.time)
            .Select(e => e.obj)
            .ToList();

        var timeline = new JsonArray(sortedEvents.ToArray());

        var result = new JsonObject
        {
            ["pipeline_run_id"] = run.Id.ToString(),
            ["workflow_id"] = run.WorkflowId,
            ["status"] = run.Status.ToString(),
            ["total_events"] = events.Count,
            ["timeline"] = timeline
        };

        return result.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
