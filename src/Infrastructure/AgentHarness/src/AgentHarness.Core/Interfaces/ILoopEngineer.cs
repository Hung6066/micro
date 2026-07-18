using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Core.Interfaces;

public interface ILoopEngineer
{
    Task<bool> EvaluateLoopContinuationAsync(PipelineRun run, LoopBackEdge loopEdge, CancellationToken ct = default);
    Task<AgentRun> ExecuteLoopIterationAsync(PipelineRun run, LoopBackEdge loopEdge, CancellationToken ct = default);
}
