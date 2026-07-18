using System.Text.Json;
using MediatR;
using Serilog;
using His.Hope.AgentHarness.Application.Commands.StartPipeline;
using His.Hope.AgentHarness.Application.Services;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

public class StartPipelineTool
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly IMediator _mediator;
    private readonly IServiceScopeFactory _scopeFactory;

    public StartPipelineTool(IMediator mediator, IServiceScopeFactory scopeFactory)
    {
        _mediator = mediator;
        _scopeFactory = scopeFactory;
    }

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var workflowId = parameters.GetValueOrDefault("workflow_id")?.ToString()
            ?? throw new ArgumentException("'workflow_id' is required.");
        var triggeredBy = parameters.GetValueOrDefault("triggered_by")?.ToString() ?? "system";

        var paramsDict = new Dictionary<string, string>();
        if (parameters.TryGetValue("params", out var paramsObj) && paramsObj != null)
        {
            Dictionary<string, object>? rawParams = null;
            if (paramsObj is Dictionary<string, object> dictParams)
                rawParams = dictParams;
            else if (paramsObj is JsonElement jsonEl && jsonEl.ValueKind == JsonValueKind.Object)
                rawParams = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonEl.GetRawText(), JsonOpts);

            if (rawParams != null)
            {
                foreach (var kvp in rawParams)
                    paramsDict[kvp.Key] = kvp.Value?.ToString() ?? "";
            }
        }

        var command = new StartPipelineCommand(workflowId, paramsDict, triggeredBy);
        var run = await _mediator.Send(command);

        // Run pipeline in background with a fresh scope
        var runId = run.Id;
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var engine = scope.ServiceProvider.GetRequiredService<IPipelineEngine>();
                var store = scope.ServiceProvider.GetRequiredService<IStateStore>();
                using var bgCts = new CancellationTokenSource(TimeSpan.FromHours(8));

                // Re-read run from store for full state
                var bgRun = await store.GetPipelineRunAsync(runId, bgCts.Token);
                if (bgRun == null)
                {
                    Log.Error("Pipeline {PipelineId} not found in store for background execution", runId);
                    return;
                }

                // Rebuild DAG from saved metadata (handles both inline and YAML tasks)
                var bgDag = new PipelineDag();
                if (bgRun.Parameters.TryGetValue("tasks", out var inlineTasks) && !string.IsNullOrEmpty(inlineTasks))
                {
                    AddTasksToDag(bgDag, inlineTasks);
                }
                else if (bgRun.Metadata.TryGetValue("resolved_tasks", out var yamlTasks) && !string.IsNullOrEmpty(yamlTasks))
                {
                    AddTasksToDag(bgDag, yamlTasks);
                }

                bgRun.SetDag(bgDag);
                Log.Information("Pipeline {PipelineId} starting in background with {NodeCount} nodes",
                    runId, bgDag.Nodes.Count);
                await engine.StartAsync(bgDag, bgRun, bgCts.Token);
                Log.Information("Pipeline {PipelineId} completed with status {Status}", runId, bgRun.Status);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Pipeline {PipelineId} background execution failed", runId);
            }
        });

        return JsonSerializer.Serialize(new
        {
            pipeline_run_id = run.Id.ToString(),
            status = run.Status.ToString(),
            workflow_id = run.WorkflowId
        });
    }

    private class PipelineTaskDef
    {
        public string Agent { get; set; } = "dotnet";
        public string Task { get; set; } = "";
        public string Phase { get; set; } = "Implement";
    }

    private void AddTasksToDag(PipelineDag dag, string tasksJson)
    {
        try
        {
            var tasks = JsonSerializer.Deserialize<List<PipelineTaskDef>>(tasksJson, JsonOpts);
            if (tasks != null)
            {
                foreach (var task in tasks)
                {
                    var phase = Enum.TryParse<PipelinePhase>(task.Phase, ignoreCase: true, out var p) ? p : PipelinePhase.Implement;
                    var node = dag.AddNode(task.Agent, phase);
                    node.TaskDescription = task.Task;
                }
            }
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "Failed to parse tasks for background pipeline");
        }
    }
}
