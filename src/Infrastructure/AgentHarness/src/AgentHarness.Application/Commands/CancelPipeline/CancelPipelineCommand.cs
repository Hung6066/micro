namespace His.Hope.AgentHarness.Application.Commands.CancelPipeline;

public record CancelPipelineCommand(Guid PipelineRunId, string Reason) : IRequest<bool>;
