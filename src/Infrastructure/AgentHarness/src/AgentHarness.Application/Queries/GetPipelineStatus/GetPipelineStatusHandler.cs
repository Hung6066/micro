namespace His.Hope.AgentHarness.Application.Queries.GetPipelineStatus;

public class GetPipelineStatusHandler : IRequestHandler<GetPipelineStatusQuery, PipelineRun?>
{
    private readonly IPipelineEngine _engine;

    public GetPipelineStatusHandler(IPipelineEngine engine) => _engine = engine;

    public async Task<PipelineRun?> Handle(GetPipelineStatusQuery request, CancellationToken ct)
    {
        return await _engine.GetStatusAsync(request.PipelineRunId, ct);
    }
}
