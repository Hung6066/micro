namespace His.Hope.AgentHarness.Application.Commands.RetryAgent;

public class RetryAgentHandler : IRequestHandler<RetryAgentCommand, AgentRun>
{
    private readonly IStateStore _store;
    private readonly IAgentDispatcher _dispatcher;

    public RetryAgentHandler(IStateStore store, IAgentDispatcher dispatcher)
    {
        _store = store;
        _dispatcher = dispatcher;
    }

    public async Task<AgentRun> Handle(RetryAgentCommand request, CancellationToken ct)
    {
        var existing = await _store.GetAgentRunAsync(request.AgentRunId, ct)
            ?? throw new InvalidOperationException($"Agent run {request.AgentRunId} not found");

        if (!existing.CanRetry())
            throw new InvalidOperationException($"Agent {existing.AgentName} has exhausted retries ({existing.RetryCount}/{existing.MaxRetries})");

        return await _dispatcher.DispatchAsync(existing, ct);
    }
}
