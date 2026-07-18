using System.Diagnostics;

namespace His.Hope.AgentHarness.Infrastructure.Observability;

public static class HarnessTracing
{
    private static readonly ActivitySource Source = new("His.Hope.AgentHarness", "1.0.0");

    public static Activity? StartPipelineActivity(string workflowId, Guid pipelineRunId)
    {
        var activity = Source.StartActivity("PipelineExecution", ActivityKind.Internal);
        activity?.SetTag("workflow.id", workflowId);
        activity?.SetTag("pipeline.run.id", pipelineRunId.ToString());
        return activity;
    }

    public static Activity? StartAgentActivity(string agentName, Guid agentRunId)
    {
        var activity = Source.StartActivity("AgentExecution", ActivityKind.Internal);
        activity?.SetTag("agent.name", agentName);
        activity?.SetTag("agent.run.id", agentRunId.ToString());
        return activity;
    }

    public static Activity? StartLoopEngineerActivity(Guid agentRunId, int iteration)
    {
        var activity = Source.StartActivity("LoopEngineerExecution", ActivityKind.Internal);
        activity?.SetTag("agent.run.id", agentRunId.ToString());
        activity?.SetTag("iteration", iteration);
        return activity;
    }
}
