namespace His.Hope.AgentHarness.Application.Commands.RetryAgent;

public record RetryAgentCommand(Guid AgentRunId) : IRequest<AgentRun>;
