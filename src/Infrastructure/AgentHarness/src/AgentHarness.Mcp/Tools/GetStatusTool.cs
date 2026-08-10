using System.Text.Json;
using MediatR;
using His.Hope.AgentHarness.Application.Queries.GetPipelineStatus;

namespace His.Hope.AgentHarness.Mcp.Tools;

public class GetStatusTool
{
    private readonly IMediator _mediator;

    public GetStatusTool(IMediator mediator) => _mediator = mediator;

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var pipelineRunIdStr = parameters.GetValueOrDefault("pipeline_run_id")?.ToString()
            ?? throw new ArgumentException("'pipeline_run_id' is required.");

        if (!Guid.TryParse(pipelineRunIdStr, out var pipelineRunId))
            throw new ArgumentException("'pipeline_run_id' must be a valid GUID.");

        var query = new GetPipelineStatusQuery(pipelineRunId);
        var run = await _mediator.Send(query);

        if (run is null)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"Pipeline run '{pipelineRunIdStr}' not found."
            });
        }

        return JsonSerializer.Serialize(new
        {
            pipeline_run_id = run.Id.ToString(),
            status = run.Status.ToString(),
            workflow_id = run.WorkflowId,
            started_at = run.StartedAt,
            completed_at = run.CompletedAt
        });
    }
}
