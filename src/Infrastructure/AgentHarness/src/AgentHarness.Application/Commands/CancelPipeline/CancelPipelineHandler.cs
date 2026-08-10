namespace His.Hope.AgentHarness.Application.Commands.CancelPipeline;

public class CancelPipelineHandler : IRequestHandler<CancelPipelineCommand, bool>
{
    private readonly IPipelineEngine _engine;

    public CancelPipelineHandler(IPipelineEngine engine) => _engine = engine;

    public async Task<bool> Handle(CancelPipelineCommand request, CancellationToken ct)
    {
        await _engine.CancelAsync(request.PipelineRunId, ct);
        return true;
    }
}
