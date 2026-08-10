namespace His.Hope.AgentHarness.Application.Queries.GetAgentRunHistory;

public record GetAgentRunHistoryQuery(Guid PipelineRunId) : IRequest<List<AgentRun>>;
