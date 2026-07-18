namespace His.Hope.AgentHarness.Application.Commands.StartPipeline;

public class StartPipelineHandler : IRequestHandler<StartPipelineCommand, PipelineRun>
{
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
        run.SetDag(dag);
        await _store.SavePipelineRunAsync(run, ct);
        await _engine.StartAsync(dag, run, ct);
        return run;
    }
}
