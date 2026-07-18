using System.Text.Json;

namespace His.Hope.AgentHarness.Application.Commands.StartPipeline;

public class StartPipelineHandler : IRequestHandler<StartPipelineCommand, PipelineRun>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly IPipelineEngine _engine;
    private readonly IStateStore _store;

    public StartPipelineHandler(IPipelineEngine engine, IStateStore store)
    {
        _engine = engine;
        _store = store;
    }

    public async Task<PipelineRun> Handle(StartPipelineCommand request, CancellationToken ct)
    {
        var dag = new PipelineDag();
        var run = PipelineRun.Create(request.WorkflowId, request.Parameters, request.TriggeredBy);

        if (request.Parameters.TryGetValue("tasks", out var tasksJson) && !string.IsNullOrEmpty(tasksJson))
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
                run.AddMetadata("dag_error", $"Failed to parse tasks: {ex.Message}");
            }
        }

        run.SetDag(dag);
        await _store.SavePipelineRunAsync(run, ct);
        await _engine.StartAsync(dag, run, ct);
        return run;
    }

    private class PipelineTaskDef
    {
        public string Agent { get; set; } = "dotnet";
        public string Task { get; set; } = "";
        public string Phase { get; set; } = "Implement";
    }
}
