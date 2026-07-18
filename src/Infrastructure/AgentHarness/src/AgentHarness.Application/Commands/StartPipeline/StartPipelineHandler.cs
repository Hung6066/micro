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
        var tasks = new List<(string Phase, string Agent, string Task, string? Condition, string? DependsOn)>();

        var workflowDef = _workflowLoader.Load(request.WorkflowId);
        if (workflowDef != null)
        {
            Log.Information("Loaded workflow '{Name}' from {Path}", workflowDef.Name, workflowDef.SourcePath);
            tasks.AddRange(workflowDef.ToTaskList().Select(t => (t.Phase, t.Agent, t.Task, (string?)null, (string?)null)));
        }
        else if (request.Parameters.TryGetValue("tasks", out var tasksJson) && !string.IsNullOrEmpty(tasksJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<PipelineTaskDef>>(tasksJson, JsonOpts);
                if (parsed != null)
                {
                    foreach (var t in parsed)
                        tasks.Add((t.Phase, t.Agent, t.Task, t.Condition, t.DependsOn));
                }
            }
            catch (JsonException ex)
            {
                run.AddMetadata("dag_error", $"Failed to parse tasks: {ex.Message}");
            }
        }

        var dag = new PipelineDag();
        var nodeMap = new Dictionary<string, PipelineNode>();

        foreach (var (phase, agent, task, condition, dependsOn) in tasks)
        {
            var phaseEnum = Enum.TryParse<PipelinePhase>(phase, ignoreCase: true, out var p) ? p : PipelinePhase.Implement;
            var node = dag.AddNode(agent, phaseEnum);
            node.TaskDescription = task;
            node.Condition = ParseCondition(condition);

            // Build edges from dependencies
            if (dependsOn != null && nodeMap.TryGetValue(dependsOn, out var depNode))
            {
                dag.AddEdge(depNode, node, node.Condition);
            }

            nodeMap[agent + "_" + task.GetHashCode()] = node;
        }

        run.SetDag(dag);
        // Save resolved tasks as 'tasks' parameter for background execution
        var resolvedTasksJson = JsonSerializer.Serialize(tasks.Select(t => new {
            phase = t.Phase, agent = t.Agent, task = t.Task,
            condition = t.Condition, depends_on = t.DependsOn
        }));
        run.AddMetadata("resolved_tasks", resolvedTasksJson);
        run.AddMetadata("task_count", tasks.Count.ToString());
        await _store.SavePipelineRunAsync(run, ct);

        return run;
    }

    private static BranchCondition ParseCondition(string? condition) => condition?.ToLowerInvariant() switch
    {
        "on_success" or "on-success" => BranchCondition.OnSuccess,
        "on_failure" or "on-failure" => BranchCondition.OnFailure,
        "never" => BranchCondition.Never,
        _ => BranchCondition.Always
    };

    private class PipelineTaskDef
    {
        public string Agent { get; set; } = "dotnet";
        public string Task { get; set; } = "";
        public string Phase { get; set; } = "Implement";
        public string? Condition { get; set; }
        public string? DependsOn { get; set; }
    }
}