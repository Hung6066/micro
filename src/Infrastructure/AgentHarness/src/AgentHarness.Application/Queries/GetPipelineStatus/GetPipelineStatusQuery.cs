namespace His.Hope.AgentHarness.Application.Queries.GetPipelineStatus;

public record GetPipelineStatusQuery(Guid PipelineRunId) : IRequest<PipelineRun?>;
