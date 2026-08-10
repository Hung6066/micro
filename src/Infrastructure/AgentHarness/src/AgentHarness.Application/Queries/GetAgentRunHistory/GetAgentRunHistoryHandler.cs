namespace His.Hope.AgentHarness.Application.Queries.GetAgentRunHistory;

public class GetAgentRunHistoryHandler : IRequestHandler<GetAgentRunHistoryQuery, List<AgentRun>>
{
    private readonly IStateStore _store;

    public GetAgentRunHistoryHandler(IStateStore store) => _store = store;

    public async Task<List<AgentRun>> Handle(GetAgentRunHistoryQuery request, CancellationToken ct)
    {
        return await _store.GetAgentRunsAsync(request.PipelineRunId, ct);
    }
}
