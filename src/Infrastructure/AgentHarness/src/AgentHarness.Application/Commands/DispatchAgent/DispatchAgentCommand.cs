namespace His.Hope.AgentHarness.Application.Commands.DispatchAgent;

public record DispatchAgentCommand(Guid PipelineRunId, string AgentName, string TaskDescription, string? ContextFrom = null, int MaxRetries = 3, int TimeoutSeconds = 600) : IRequest<AgentRun>;
