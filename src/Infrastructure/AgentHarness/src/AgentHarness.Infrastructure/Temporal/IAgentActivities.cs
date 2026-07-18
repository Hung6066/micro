using Temporalio.Activities;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Infrastructure.Temporal;

public class PhaseResult
{
    public int NodeCount { get; set; }
    public bool AllCompleted { get; set; }
}

public class LoopEngineerResult
{
    public bool CanContinue { get; set; }
    public string? EscalationReason { get; set; }
}

public interface IAgentActivities
{
    [Activity]
    Task<PhaseResult> ExecutePhaseAsync(Guid pipelineRunId, string phaseName, int loopIteration);

    [Activity]
    Task EvaluatePhaseGatesAsync(Guid pipelineRunId, string phaseName);

    [Activity]
    Task SaveCheckpointAsync(Guid pipelineRunId, string phaseName, int loopIteration);

    [Activity]
    Task<bool> CheckAllGatesPassedAsync(Guid pipelineRunId);

    [Activity]
    Task<LoopEngineerResult> RunLoopEngineerAsync(Guid pipelineRunId, int loopIteration);

    [Activity]
    Task ResetFailedNodesAsync(Guid pipelineRunId, PipelineDag dag);
}
