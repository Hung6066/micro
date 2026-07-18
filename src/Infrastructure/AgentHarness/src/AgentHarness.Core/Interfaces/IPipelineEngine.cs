using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Core.Interfaces;

public interface IPipelineEngine
{
    Task<PipelineRun> StartAsync(PipelineDag dag, PipelineRun run, CancellationToken ct = default);
    Task CancelAsync(Guid pipelineRunId, CancellationToken ct = default);
    Task<PipelineRun> GetStatusAsync(Guid pipelineRunId, CancellationToken ct = default);
}
