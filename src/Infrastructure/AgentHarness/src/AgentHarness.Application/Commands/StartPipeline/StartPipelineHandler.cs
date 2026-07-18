using System.Text.Json;
using Serilog;

namespace His.Hope.AgentHarness.Application.Commands.StartPipeline;

public class StartPipelineHandler : IRequestHandler<StartPipelineCommand, PipelineRun>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly IStateStore _store;
    private readonly WorkflowLoader _workflowLoader;

    public StartPipelineHandler(IStateStore store, WorkflowLoader workflowLoader)
    {
        _store = store;
        _workflowLoader = workflowLoader;
    }

    public async Task<PipelineRun> Handle(StartPipelineCommand request, CancellationToken ct)
    {
        var run = PipelineRun.Create(request.WorkflowId, request.Parameters, request.TriggeredBy);

        // Load tasks from YAML workflow or inline tasks
        var tasks = new List<(string Phase, string Agent, string Task)>();

        var workflowDef = _workflowLoader.Load(request.WorkflowId);
        if (workflowDef != null)
        {
            Log.Information("Loaded workflow '{Name}' from {Path}", workflowDef.Name, workflowDef.SourcePath);
            tasks.AddRange(workflowDef.ToTaskList());
        }
        else if (request.Parameters.TryGetValue("tasks", out var tasksJson) && !string.IsNullOrEmpty(tasksJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<PipelineTaskDef>>(tasksJson, JsonOpts);
                if (parsed != null)
                {
                    foreach (var t in parsed)
                        tasks.Add((t.Phase, t.Agent, t.Task));
                }
            }
            catch (JsonException ex)
            {
                run.AddMetadata("dag_error", $"Failed to parse tasks: {ex.Message}");
            }
        }

        var dag = new PipelineDag();
        foreach (var (phase, agent, task) in tasks)
        {
            var phaseEnum = Enum.TryParse<PipelinePhase>(phase, ignoreCase: true, out var p) ? p : PipelinePhase.Implement;
            var node = dag.AddNode(agent, phaseEnum);
            node.TaskDescription = task;
        }

        run.SetDag(dag);
        // Save resolved tasks as 'tasks' parameter for background execution
        var resolvedTasksJson = JsonSerializer.Serialize(tasks.Select(t => new { phase = t.Phase, agent = t.Agent, task = t.Task }));
        run.AddMetadata("resolved_tasks", resolvedTasksJson);
        run.AddMetadata("task_count", tasks.Count.ToString());
        await _store.SavePipelineRunAsync(run, ct);

        return run;
    }

    private class PipelineTaskDef
    {
        public string Agent { get; set; } = "dotnet";
        public string Task { get; set; } = "";
        public string Phase { get; set; } = "Implement";
    }
}