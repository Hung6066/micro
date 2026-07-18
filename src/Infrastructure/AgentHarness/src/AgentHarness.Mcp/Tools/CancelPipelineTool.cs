using System.Text.Json;
using MediatR;
using His.Hope.AgentHarness.Application.Commands.CancelPipeline;

namespace His.Hope.AgentHarness.Mcp.Tools;

public class CancelPipelineTool
{
    private readonly IMediator _mediator;

    public CancelPipelineTool(IMediator mediator) => _mediator = mediator;

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var pipelineRunIdStr = parameters.GetValueOrDefault("pipeline_run_id")?.ToString()
            ?? throw new ArgumentException("'pipeline_run_id' is required.");
        var reason = parameters.GetValueOrDefault("reason")?.ToString() ?? "Cancelled by user";

        if (!Guid.TryParse(pipelineRunIdStr, out var pipelineRunId))
            throw new ArgumentException("'pipeline_run_id' must be a valid GUID.");

        var command = new CancelPipelineCommand(pipelineRunId, reason);
        var success = await _mediator.Send(command);

        return JsonSerializer.Serialize(new
        {
            success,
            message = success
                ? $"Pipeline '{pipelineRunIdStr}' cancelled successfully."
                : $"Failed to cancel pipeline '{pipelineRunIdStr}'."
        });
    }
}
