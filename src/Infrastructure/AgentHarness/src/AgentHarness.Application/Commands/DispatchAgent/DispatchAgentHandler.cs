namespace His.Hope.AgentHarness.Application.Commands.DispatchAgent;

public class DispatchAgentHandler : IRequestHandler<DispatchAgentCommand, AgentRun>
{
    private readonly IAgentDispatcher _dispatcher;
    private readonly IStateStore _store;

    public DispatchAgentHandler(IAgentDispatcher dispatcher, IStateStore store)
    {
        _dispatcher = dispatcher;
        _store = store;
    }

    public async Task<AgentRun> Handle(DispatchAgentCommand request, CancellationToken ct)
    {
        var agentRun = AgentRun.Create(request.PipelineRunId, request.AgentName, request.TaskDescription, request.MaxRetries, request.TimeoutSeconds);
        await _store.SaveAgentRunAsync(agentRun, ct);
        return await _dispatcher.DispatchAsync(agentRun, ct);
    }
}
