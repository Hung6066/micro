namespace His.Hope.AgentHarness.Application.Commands.StartPipeline;

public record StartPipelineCommand(string WorkflowId, Dictionary<string, string> Parameters, string TriggeredBy) : IRequest<PipelineRun>;
