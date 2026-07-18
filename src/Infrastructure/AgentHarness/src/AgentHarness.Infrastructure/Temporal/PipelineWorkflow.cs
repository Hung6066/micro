using Temporalio.Common;
using Temporalio.Workflows;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Infrastructure.Temporal;

[Workflow]
public class PipelineWorkflow
{
    private static readonly string[] PhaseOrder =
        { "Plan", "Implement", "Test", "Validate", "Commit" };

    private const int MaxLoops = 3;

    [WorkflowRun]
    public async Task<PipelineRun> ExecutePipelineAsync(PipelineRun run)
    {
        var dag = run.DagDefinition;
        if (dag == null)
        {
            run.TransitionTo(PipelineStatus.Failed);
            run.AddMetadata("error", "No DAG definition provided");
            return run;
        }

        run.TransitionTo(PipelineStatus.Running);

        for (int loop = 0; loop < MaxLoops; loop++)
        {
            bool anyExecuted = false;

            foreach (var phaseName in PhaseOrder)
            {
                var phase = Enum.Parse<PipelinePhase>(phaseName);

                var phaseResult = await Workflow.ExecuteActivityAsync(
                    (IAgentActivities a) => a.ExecutePhaseAsync(run.Id, phaseName, loop),
                    new ActivityOptions
                    {
                        StartToCloseTimeout = TimeSpan.FromHours(2),
                        RetryPolicy = new RetryPolicy
                        {
                            InitialInterval = TimeSpan.FromSeconds(1),
                            MaximumInterval = TimeSpan.FromSeconds(30),
                            BackoffCoefficient = 2,
                            MaximumAttempts = 3
                        },
                        HeartbeatTimeout = TimeSpan.FromSeconds(30)
                    });

                if (phaseResult.NodeCount > 0)
                    anyExecuted = true;

                // Evaluate quality gates after phase
                await Workflow.ExecuteActivityAsync(
                    (IAgentActivities a) => a.EvaluatePhaseGatesAsync(run.Id, phaseName),
                    new ActivityOptions
                    {
                        StartToCloseTimeout = TimeSpan.FromMinutes(5),
                        RetryPolicy = new RetryPolicy { MaximumAttempts = 2 }
                    });

                // Save checkpoint
                await Workflow.ExecuteActivityAsync(
                    (IAgentActivities a) => a.SaveCheckpointAsync(run.Id, phaseName, loop),
                    new ActivityOptions
                    {
                        StartToCloseTimeout = TimeSpan.FromMinutes(2),
                        RetryPolicy = new RetryPolicy { MaximumAttempts = 2 }
                    });
            }

            if (!anyExecuted)
            {
                run.TransitionTo(PipelineStatus.Completed);
                return run;
            }

            // Check all gates
            var gatesPassed = await Workflow.ExecuteActivityAsync(
                (IAgentActivities a) => a.CheckAllGatesPassedAsync(run.Id),
                new ActivityOptions
                {
                    StartToCloseTimeout = TimeSpan.FromMinutes(2),
                    RetryPolicy = new RetryPolicy { MaximumAttempts = 2 }
                });

            if (gatesPassed)
            {
                run.TransitionTo(PipelineStatus.Completed);
                return run;
            }

            // Loop Engineer iteration
            if (loop < MaxLoops - 1)
            {
                var fixResult = await Workflow.ExecuteActivityAsync(
                    (IAgentActivities a) => a.RunLoopEngineerAsync(run.Id, loop),
                    new ActivityOptions
                    {
                        StartToCloseTimeout = TimeSpan.FromMinutes(30),
                        RetryPolicy = new RetryPolicy
                        {
                            InitialInterval = TimeSpan.FromSeconds(5),
                            MaximumAttempts = 2
                        }
                    });

                if (!fixResult.CanContinue)
                {
                    run.TransitionTo(PipelineStatus.Failed);
                    run.AddMetadata("error", fixResult.EscalationReason ?? "Pipeline blocked after loop engineer");
                    return run;
                }

                // Reset failed nodes for retry
                await Workflow.ExecuteActivityAsync(
                    (IAgentActivities a) => a.ResetFailedNodesAsync(run.Id, dag),
                    new ActivityOptions
                    {
                        StartToCloseTimeout = TimeSpan.FromMinutes(2)
                    });
            }
            else
            {
                run.TransitionTo(PipelineStatus.Failed);
                run.AddMetadata("error", "Max pipeline loops reached");
                return run;
            }
        }

        run.TransitionTo(PipelineStatus.Completed);
        return run;
    }
}
