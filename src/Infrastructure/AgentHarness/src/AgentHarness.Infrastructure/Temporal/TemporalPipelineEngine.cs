using Serilog;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Infrastructure.Temporal;

public class TemporalPipelineEngine : IPipelineEngine
{
    private readonly ITemporalClient _client;
    private const string TaskQueue = "agent-harness";

    public TemporalPipelineEngine(ITemporalClient client)
    {
        _client = client;
    }

    public async Task<PipelineRun> StartAsync(PipelineDag dag, PipelineRun run, CancellationToken ct = default)
    {
        run.SetDag(dag);
        run.TransitionTo(PipelineStatus.Running);

        var workflowId = $"pipeline-{run.Id}";

        try
        {
            await _client.StartWorkflowAsync(
                (PipelineWorkflow wf) => wf.ExecutePipelineAsync(run),
                new WorkflowOptions
                {
                    Id = workflowId,
                    TaskQueue = TaskQueue,
                    ExecutionTimeout = TimeSpan.FromHours(8)
                });

            Log.Information("Temporal workflow started: {WorkflowId} for pipeline {PipelineId}",
                workflowId, run.Id);

            return run;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start Temporal workflow for pipeline {PipelineId}", run.Id);
            run.TransitionTo(PipelineStatus.Failed);
            run.AddMetadata("error", $"Temporal start failed: {ex.Message}");
            return run;
        }
    }

    public async Task<PipelineRun> ResumeAsync(PipelineRun run, CancellationToken ct = default)
    {
        var workflowId = $"pipeline-{run.Id}";

        try
        {
            var handle = _client.GetWorkflowHandle(workflowId);
            var desc = await handle.DescribeAsync();

            if (desc.Status == WorkflowExecutionStatus.Running)
            {
                Log.Information("Temporal workflow {WorkflowId} already running, reusing", workflowId);
                return run;
            }

            return await StartAsync(run.DagDefinition!, run, ct);
        }
        catch (RpcException ex) when (ex.Code == RpcException.StatusCode.NotFound || ex.Code == RpcException.StatusCode.Unavailable)
        {
            Log.Information("Temporal workflow {WorkflowId} not found, starting fresh", workflowId);
            return await StartAsync(run.DagDefinition!, run, ct);
        }
    }

    public async Task CancelAsync(Guid pipelineRunId, CancellationToken ct = default)
    {
        var workflowId = $"pipeline-{pipelineRunId}";
        try
        {
            var handle = _client.GetWorkflowHandle(workflowId);
            await handle.CancelAsync();
            Log.Information("Temporal workflow cancelled: {WorkflowId}", workflowId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to cancel Temporal workflow {WorkflowId}", workflowId);
        }
    }

    public async Task<PipelineRun> GetStatusAsync(Guid pipelineRunId, CancellationToken ct = default)
    {
        var workflowId = $"pipeline-{pipelineRunId}";
        try
        {
            var handle = _client.GetWorkflowHandle(workflowId);
            var desc = await handle.DescribeAsync();

            var status = desc.Status switch
            {
                WorkflowExecutionStatus.Running => PipelineStatus.Running,
                WorkflowExecutionStatus.Completed => PipelineStatus.Completed,
                WorkflowExecutionStatus.Failed or
                WorkflowExecutionStatus.TimedOut => PipelineStatus.Failed,
                WorkflowExecutionStatus.Canceled => PipelineStatus.Cancelled,
                WorkflowExecutionStatus.Terminated => PipelineStatus.Cancelled,
                _ => PipelineStatus.Pending
            };

            var run = PipelineRun.Create("temporal", new(), "temporal");
            run.TransitionTo(status);

            return run;
        }
        catch (RpcException ex) when (ex.Code == RpcException.StatusCode.NotFound || ex.Code == RpcException.StatusCode.Unavailable)
        {
            throw new InvalidOperationException($"Pipeline run {pipelineRunId} not found in Temporal");
        }
    }
}
